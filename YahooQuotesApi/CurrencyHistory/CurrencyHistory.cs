using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    public class CurrencyHistory
    {
        private readonly HttpClient HttpClient;
        private readonly BoeCurrencyHistory BoeCurrency;
        private readonly AsyncLazyCache<string, List<RateTick>> Cache;
        private readonly LocalDate Start, End;
        private readonly ILogger Logger;
        public IReadOnlyDictionary<string, string> Symbols { get; }

        public CurrencyHistory(LocalDate start, LocalDate end, ILogger<CurrencyHistory>? logger = null, HttpClient? httpClient = null)
        {
            Logger = logger ?? NullLogger<CurrencyHistory>.Instance;
            if (start.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant() > Utility.Clock.GetCurrentInstant())
                throw new ArgumentException("start > now");
            if (start > end)
                throw new ArgumentException("start > end");
            Start = start;
            End = end;
            HttpClient = httpClient ?? new HttpClient();
            BoeCurrency = new BoeCurrencyHistory(Start, End, Logger, HttpClient);
            Cache = new AsyncLazyCache<string, List<RateTick>>(BoeCurrency.Retrieve);
            Symbols = BoeCurrencyHistory.Symbols.ToDictionary(k => k.Key, k => k.Value.name);
        }
        public CurrencyHistory(LocalDate start, ILogger<CurrencyHistory>? logger = null) :
            this(start, LocalDate.MaxIsoValue, logger) { }
        public CurrencyHistory(int days, ILogger<CurrencyHistory>? logger = null) :
            this(Utility.Clock.GetCurrentInstant().Minus(Duration.FromDays(days)).InUtc().Date, LocalDate.MaxIsoValue, logger) {}
        public CurrencyHistory(ILogger<CurrencyHistory>? logger = null) : this(LocalDate.MinIsoValue, LocalDate.MaxIsoValue, logger) { }

        public async Task<Dictionary<string, List<RateTick>?>> GetRatesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
        {
            if (symbols == null)
                throw new ArgumentNullException(nameof(symbols));
            if (!symbols.Any())
                return new Dictionary<string, List<RateTick>?>();
            var tasks = symbols.Select(symbol => GetRatesAsync(symbol, ct));
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return symbols.Zip(tasks, (symbol, task) => (symbol, task.Result))
                .ToDictionary(x => x.symbol, x => x.Result);
        }

        public async Task<List<RateTick>?> GetRatesAsync(string symbol, CancellationToken ct = default) // USDJPY=X 80
        {
            if (!CurrencyUtility.IsCurrencySymbolFormat(symbol))
                throw new ArgumentException(nameof(symbol));
            if (!CurrencyUtility.IsCurrencySymbol(symbol))
                return null;
            string symbol1 = symbol.Substring(0, 3), symbol2 = symbol.Substring(3, 3);
            if (symbol1 == symbol2)
                throw new ArgumentException($"Invalid currency symbol: {symbol}.");
            return await GetComponents(symbol1, symbol2, ct).ConfigureAwait(false);
        }

        private async Task<List<RateTick>> GetComponents(string symbol1, string symbol2, CancellationToken ct)
        {
            var task1 = (symbol1 != "USD") ? Cache.Get(symbol1, ct) : null;
            var task2 = (symbol2 != "USD") ? Cache.Get(symbol2, ct) : null;
            var rates1 = (task1 != null) ? await task1.ConfigureAwait(false) : null;
            var rates2 = (task2 != null) ? await task2.ConfigureAwait(false) : null;
            if (rates1 == null)
            {
                if (rates2 == null)
                    throw new InvalidOperationException();
                return rates2;
            }
            if (rates2 == null)
                return rates1.Select(r => new RateTick(r.Date, 1m / r.Rate)).ToList(); // invert
            var comboRates = new List<RateTick>();
            foreach (var tick in rates2)
            {
                var rate = InterpolateRate(rates1, tick.Date);
                if (rate != decimal.MinusOne)
                    comboRates.Add(new RateTick(tick.Date, tick.Rate / rate));
            }
            return comboRates;
        }

        private decimal InterpolateRate(List<RateTick> list, LocalDate date, int tryIndex = -1)
        {
            var len = list.Count;
            if (tryIndex != -1 && date == list[tryIndex].Date)
                return list[tryIndex].Rate;
            if (date < list[0].Date) // not enough data
                return decimal.MinusOne;
            var days = (date - list[len - 1].Date).Days; // future date so use the latest data
            if (days >= 0)
            {
                if (days < 4)
                    return list[len - 1].Rate;
                return decimal.MinusOne;
            }
            var p = list.BinarySearch(new RateTick(date, decimal.MinusOne));
            if (p >= 0) // found
                return list[p].Rate;
            p = ~p; // not found, ~p is next highest position in list; linear interpolation
            var rate = list[p].Rate + ((list[p - 1].Rate - list[p].Rate) / (list[p - 1].Date - list[p].Date).Ticks * (date - list[p].Date).Ticks);
            Logger.LogInformation($"InterpolateRate: {date} => {p} => {rate}.");
            return rate;
        }
    }
}

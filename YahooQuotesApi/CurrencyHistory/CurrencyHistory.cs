using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    public class CurrencyHistory
    {
        private readonly ILogger Logger;
        private readonly HttpClient HttpClient;
        private LocalDate StartDate = LocalDate.MinIsoValue;
        private BoeCurrencyHistory BoeCurrency;
        private AsyncLazyCache<string, List<RateTick>> Cache;
        public static IReadOnlyDictionary<string, string> Symbols { get; } = 
            BoeCurrencyHistory.Symbols.ToDictionary(k => k.Key, k => k.Value.name);

        public CurrencyHistory(ILogger<CurrencyHistory>? logger = null, HttpClient? httpClient = null)
        {
            Logger = logger ?? NullLogger<CurrencyHistory>.Instance;
            HttpClient = httpClient ?? new HttpClient();
            BoeCurrency = new BoeCurrencyHistory(StartDate, Logger, HttpClient);
            Cache = new AsyncLazyCache<string, List<RateTick>>(BoeCurrency.Retrieve);
        }
        public CurrencyHistory FromDate(LocalDate start)
        {
            StartDate = start;
            BoeCurrency = new BoeCurrencyHistory(StartDate, Logger, HttpClient);
            Cache = new AsyncLazyCache<string, List<RateTick>>(BoeCurrency.Retrieve);
            return this;
        }

        /*
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
        */

        public async Task<List<RateTick>> GetRatesAsync(string symbolA, string symbolB, CancellationToken ct = default)
        {
            if (symbolA == null || !BoeCurrencyHistory.Symbols.ContainsKey(symbolA))
                throw new ArgumentException(nameof(symbolA));
            if (symbolB == null || !BoeCurrencyHistory.Symbols.ContainsKey(symbolB))
                throw new ArgumentException(nameof(symbolB));
            if (symbolA == symbolB)
                throw new ArgumentException($"Invalid currency symbol combination: {symbolA}={symbolB}.");

            var taskA = (symbolA != "USD") ? Cache.Get(symbolA, ct) : null;
            var taskB = (symbolB != "USD") ? Cache.Get(symbolB, ct) : null;
            var ratesA = (taskA != null) ? await taskA.ConfigureAwait(false) : null;
            var ratesB = (taskB != null) ? await taskB.ConfigureAwait(false) : null;
            if (ratesA == null)
            {
                if (ratesB == null)
                    throw new InvalidOperationException();
                return ratesB;
            }
            if (ratesB == null)
                return ratesA.Select(r => new RateTick(r.Date, 1d / r.Rate)).ToList(); // invert
            var comboRates = new List<RateTick>();
            foreach (var tick in ratesB)
            {
                var rate = InterpolateRate(ratesA, tick.Date);
                if (rate != null)
                    comboRates.Add(new RateTick(tick.Date, tick.Rate / rate.Value));
            }
            return comboRates;
        }

        private double? InterpolateRate(List<RateTick> list, Instant date, int tryIndex = -1)
        {
            if (tryIndex != -1 && date == list[tryIndex].Date)
                return list[tryIndex].Rate;
            if (date < list[0].Date) // not enough data
                return null;
            var last = list[list.Count - 1];
            var days = (date - last.Date).Days;
            if (days >= 0) // future date, so use the latest data
            {
                if (days > 7)
                    return null;
                return last.Rate;
            }
            var p = list.BinarySearch(new RateTick(date, -1));
            if (p >= 0) // found
                return list[p].Rate;
            p = ~p; // not found, ~p is next highest position in list; linear interpolation
            var rate = list[p].Rate + ((list[p - 1].Rate - list[p].Rate) / (list[p - 1].Date - list[p].Date).TotalTicks * (date - list[p].Date).TotalTicks);
            Logger.LogInformation($"InterpolateRate: {date} => {p} => {rate}.");
            return rate;
        }
    }
}

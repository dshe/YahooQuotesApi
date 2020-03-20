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
        public static LocalTime SpotTime { get; } = BoeCurrencyHistory.SpotTime;
        public static DateTimeZone TimeZone { get; } = BoeCurrencyHistory.TimeZone;
        public static IReadOnlyDictionary<string, string> Symbols { get; } = 
            BoeCurrencyHistory.Symbols.ToDictionary(k => k.Key, k => k.Value.name);
        private readonly ILogger Logger;
        private readonly AsyncLazyCache<string, List<RateTick>> Cache;
        private readonly BoeCurrencyHistory BoeCurrency;

        public CurrencyHistory(ILogger<CurrencyHistory>? logger = null, HttpClient? httpClient = null)
        {
            Logger = logger ?? NullLogger<CurrencyHistory>.Instance;
            BoeCurrency = new BoeCurrencyHistory(Logger, httpClient ??= new HttpClient());
            Cache = new AsyncLazyCache<string, List<RateTick>>(BoeCurrency.Retrieve);
        }

        public CurrencyHistory FromDate(LocalDate start)
        {
            BoeCurrency.SetStartDate(start);
            Cache.Clear();
            return this;
        }

        public async Task<IReadOnlyList<RateTick>> GetRatesAsync(string symbol, string symbolBase, CancellationToken ct = default)
        {
            if (symbol == null || !BoeCurrencyHistory.Symbols.ContainsKey(symbol))
                throw new ArgumentException(nameof(symbol));
            if (symbolBase == null || !BoeCurrencyHistory.Symbols.ContainsKey(symbolBase))
                throw new ArgumentException(nameof(symbolBase));

            var task     = (symbol     != "USD") ? Cache.Get(symbol,     ct) : null;
            var taskBase = (symbolBase != "USD") ? Cache.Get(symbolBase, ct) : null;

            var rates     = (task     != null) ? await     task.ConfigureAwait(false) : null;
            var ratesBase = (taskBase != null) ? await taskBase.ConfigureAwait(false) : null;

            if (rates == null)
            {
                if (ratesBase == null)
                    throw new ArgumentException($"Invalid currency pair: {symbol}={symbolBase}.");
                return ratesBase;
            }
            if (ratesBase == null)
                return rates.Select(r => new RateTick(r.Date, 1d / r.Rate)).ToList(); // invert

            return ratesBase
                .Select(tick => (tick, rate: InterpolateRate(rates, tick.Date)))
                .Where(item => item.rate != null)
                .Select(item => new RateTick(item.tick.Date, item.tick.Rate / item.rate!.Value))
                .ToList();
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

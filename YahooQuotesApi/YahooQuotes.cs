using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// Invalid symbols are often, but not always, ignored by Yahoo.
// So the number of symbols returned may be less than requested.
// When multiple symbols are requested here, null is returned for invalid symbols.

// test duplicate symbols
// check that same symbol is returned. Valid?
// make sure symbols have a currency, and a symbol

namespace YahooQuotesApi
{
    public sealed class YahooQuotes
    {
        private static readonly LocalTime CurrencyCloseTime = new LocalTime(16, 0, 0);
        private static readonly DateTimeZone CurrencyTimezone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/London")!;
        private readonly ILogger Logger;
        private readonly HistoryFlags HistoryFlags;
        private readonly Frequency PriceHistoryFrequency;
        private readonly string PriceHistoryBaseCurrency;
        private readonly Snapshot Snapshot;
        private readonly History History;

        internal YahooQuotes(ILogger logger, HistoryFlags historyFlags, Frequency priceHistoryFrequency, string baseCurrency, Instant historyStart, Duration cacheDuration)
        {
            Logger = logger;
            HistoryFlags = historyFlags;
            PriceHistoryFrequency = priceHistoryFrequency;
            PriceHistoryBaseCurrency = baseCurrency;
            Snapshot = new Snapshot(logger);
            History = new History(logger, historyStart, cacheDuration);
        }

        public async Task<Security?> GetAsync(string symbol, CancellationToken ct = default) =>
            (await GetAsync(new[] { symbol }, ct).ConfigureAwait(false)).Values.Single();

        public async Task<IReadOnlyDictionary<string, Security?>> GetAsync(IEnumerable<string> symbols, CancellationToken ct = default)
        {
            var symbolsChecked = symbols.CheckSymbols().ToList();
            var snapshots = await GetAsyncDictionary(symbolsChecked, ct).ConfigureAwait(false);
            return snapshots.ToDictionary(kvp => kvp.Key, kvp => kvp.Value == null ? null : new Security(kvp.Value), StringComparer.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<string, Dictionary<string, object>?>> GetAsyncDictionary(List<string> symbols, CancellationToken ct)
        {
            var snapshots = await Snapshot.GetAsync(symbols, ct).ConfigureAwait(false);
            if (HistoryFlags == HistoryFlags.None)
                return snapshots;

            var currencyRateSnapshotsTask = GetCurrencyRateSnapshots(snapshots, ct);

            // start history tasks
            foreach (var kvp in snapshots)
            {
                var symbol = kvp.Key;
                var dict = kvp.Value;
                if (dict == null)
                    continue;
                if (HistoryFlags.HasFlag(HistoryFlags.PriceHistory))
                {
                    var closeTime = (LocalTime) dict["ExchangeCloseTime"];
                    var tz = (DateTimeZone) dict["ExchangeTimezone"];
                    dict.Add("PriceHistory", History.GetPricesAsync(symbol, PriceHistoryFrequency, closeTime, tz, ct));
                }
                if (HistoryFlags.HasFlag(HistoryFlags.DividendHistory))
                    dict.Add("DividendHistory", History.GetDividendsAsync(symbol, ct));
                if (HistoryFlags.HasFlag(HistoryFlags.SplitHistory))
                    dict.Add("SplitHistory", History.GetSplitsAsync(symbol, ct));
            }

            var currencyRateSnapshots = await currencyRateSnapshotsTask.ConfigureAwait(false);

            // await history tasks
            foreach (var dict in snapshots.Values.WhereNotNull())
            {
                if (HistoryFlags.HasFlag(HistoryFlags.PriceHistory))
                {
                    dynamic task = dict["PriceHistory"];
                    dict["PriceHistory"] = await task.ConfigureAwait(false);
                    ModifyPriceHistory(dict, currencyRateSnapshots);
                }
                if (HistoryFlags.HasFlag(HistoryFlags.DividendHistory))
                {
                    dynamic task = dict["DividendHistory"];
                    dict["DividendHistory"] = await task.ConfigureAwait(false);
                }
                if (HistoryFlags.HasFlag(HistoryFlags.SplitHistory))
                {
                    dynamic task = dict["SplitHistory"];
                    dict["SplitHistory"] = await task.ConfigureAwait(false); ;
                }
            }

            return snapshots;
        }

        private async Task<Dictionary<string, Dictionary<string, object>?>> 
            GetCurrencyRateSnapshots(Dictionary<string, Dictionary<string, object>?> snapshots, CancellationToken ct)
        {
            var rateSymbols = GetRatesToRetrieve(snapshots);
            if (!rateSymbols.Any())
                return new Dictionary<string, Dictionary<string, object>?>();

            // start currency history tasks
            var currencyHistoryTasks = rateSymbols
                .Select(s => History.GetPricesAsync(s, Frequency.Daily, CurrencyCloseTime, CurrencyTimezone, ct))
                .ToList();

            var currencySnapshots = await Snapshot.GetAsync(rateSymbols, ct).ConfigureAwait(false);

            // await currency history tasks
            for (var i = 0; i < rateSymbols.Count; i++)
            {
                var symbol = rateSymbols[i];
                if (!currencySnapshots.TryGetValue(symbol, out var snapshot))
                    throw new InvalidOperationException($"Symbol not found: {symbol}.");
                if (snapshot == null)
                    throw new InvalidOperationException($"No data for symbol: {symbol}.");
                var ticks = await currencyHistoryTasks[i].ConfigureAwait(false);
                snapshot.Add("PriceHistory", ticks);
                ModifyPriceHistory(snapshot);
            }

            return currencySnapshots;
        }

        private List<string> GetRatesToRetrieve(Dictionary<string, Dictionary<string, object>?> snapshots)
        {
            if (PriceHistoryBaseCurrency == "")
                return new List<string>();

            var currencies = snapshots.Values
                .WhereNotNull()
                .Select(d => (string)d["Currency"])
                .Where(c => !string.IsNullOrEmpty(c))
                .Append(PriceHistoryBaseCurrency)
                .Select(c => c.ToUpper())
                .Distinct()
                .ToList();

            if (currencies.Count == 1)
                return new List<string>();

            return currencies
                .Where(c => c != "USD")
                .Select(c => $"USD{c}=X")
                .Where(c => !snapshots.ContainsKey(c)) // in case already retrieved
                .ToList();
        }

        private void ModifyPriceHistory(Dictionary<string, object> snapshot)
            => ModifyPriceHistory(snapshot, new Dictionary<string, Dictionary<string, object>?>());

        private void ModifyPriceHistory(Dictionary<string, object> snapshot,
            Dictionary<string, Dictionary<string, object>?> currencySnapshots)
        {
            var ticks = (IReadOnlyList<PriceTick>) snapshot["PriceHistory"];
            var date = (ZonedDateTime) snapshot["RegularMarketTime"];
            var appendSnapshot = date.ToInstant() <= ticks.Last().Date.ToInstant();
            if (!currencySnapshots.Any())
            {
                if (appendSnapshot)
                {
                    snapshot["PriceHistory"] = new List<PriceTick>(ticks)
                    {
                        new PriceTick(snapshot, 1)
                    };
                }
                return;
            }

            var currency = (string) snapshot["Currency"];
            IReadOnlyList<PriceTick>? currencyRates = GetRates(currency, currencySnapshots);
            IReadOnlyList<PriceTick>? baseCurrencyRates = GetRates(PriceHistoryBaseCurrency, currencySnapshots);

            var newTicks = new List<PriceTick>(ticks.Count + 1);
            foreach (var tick in ticks)
            {
                var rate = GetRate(tick.Date);
                if (!double.IsNaN(rate))
                    newTicks.Add(new PriceTick(tick, rate));
            }

            if (appendSnapshot)
            {
                var zdt = (ZonedDateTime) snapshot["RegularMarketTime"];
                var rate = GetRate(zdt);
                if (!double.IsNaN(rate))
                    newTicks.Add(new PriceTick(snapshot, rate));
            }

            snapshot["PriceHistory"] = newTicks;

            // local function
            double GetRate(ZonedDateTime date)
            {
                var instant = date.ToInstant();
                var currencyRate = currencyRates?.Interpolate(instant);
                var baseCurrencyRate = baseCurrencyRates?.Interpolate(instant);
                double rate = 1;
                if (currencyRate != null)
                {
                    var r = currencyRate.Value;
                    if (double.IsNaN(r))
                        return double.NaN;
                    //rate *= currencyRate.Value;
                    rate /= currencyRate.Value;
                }
                if (baseCurrencyRate != null)
                {
                    var r = baseCurrencyRate.Value;
                    if (double.IsNaN(r))
                        return double.NaN;
                    //rate /= baseCurrencyRate.Value;
                    rate *= baseCurrencyRate.Value;
                }
                return rate;
            }
        }

        private List<PriceTick>? GetRates(string currency, Dictionary<string, Dictionary<string, object>?> currencySnapshots)
        {
            if (currency == "USD")
                return null;
            var symbol = $"USD{currency}=X";
            if (!currencySnapshots.TryGetValue(symbol, out var snapshot))
                throw new ArgumentException($"CurrencySnapshot not found: {symbol}.");
            if (snapshot == null)
                throw new ArgumentException($"CurrencySnapshot null: {symbol}.");
            ModifyPriceHistory(snapshot);
            return (List<PriceTick>) snapshot["PriceHistory"];
        }
    }

}

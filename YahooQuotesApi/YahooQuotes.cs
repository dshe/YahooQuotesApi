using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
        private readonly Snapshot Snapshot;
        private readonly History History;

        internal YahooQuotes(ILogger logger, HistoryFlags historyFlags, Instant historyStart, Duration cacheDuration, Frequency priceHistoryFrequency)
        {
            Logger = logger;
            HistoryFlags = historyFlags;
            Snapshot = new Snapshot(logger);
            History = new History(logger, historyStart, cacheDuration, priceHistoryFrequency);
        }

        public async Task<Security?> GetAsync(string symbol, string historyBase = "", CancellationToken ct = default) =>
            (await GetAsync(new[] { symbol }, historyBase, ct).ConfigureAwait(false)).Values.Single();

        public async Task<IReadOnlyDictionary<string, Security?>> GetAsync(IEnumerable<string> symbols, string historyBase = "", CancellationToken ct = default)
        {
            var symbolsChecked = symbols.Select(s => s.CheckSymbol()).Distinct().ToList();
            if (symbolsChecked.Any(s => s.EndsWith("=X") && (s.Length != 8 || s.Substring(0, 3) == s.Substring(3, 3))))
                throw new ArgumentException("Currency rates must in the form ABCEFG=X.");
            if (historyBase != "")
            {
                if (!HistoryFlags.HasFlag(HistoryFlags.PriceHistory))
                    throw new ArgumentException("PriceHistory must be enabled before specifying historyBase.");
                historyBase = historyBase.CheckSymbol();
                if (historyBase.EndsWith("=X"))
                {
                    if (historyBase.Length != 5)
                        throw new ArgumentException("History base currency must in the form ABC=X.");
                }
            }
            var snapshots = await GetAsyncDictionary(symbolsChecked, historyBase, ct).ConfigureAwait(false);
            return snapshots.ToDictionary(kvp => kvp.Key, kvp => kvp.Value == null ? null : new Security(kvp.Value), StringComparer.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<string, Dictionary<string, object>?>> GetAsyncDictionary(List<string> symbols, string historyBase, CancellationToken ct)
        {
            var snapshotSymbols = new List<string>(symbols);
            if (historyBase != "" && !historyBase.EndsWith("=X") && !snapshotSymbols.Contains(historyBase))
                snapshotSymbols.Add(historyBase);
            var snapshots = await Snapshot.GetAsync(snapshotSymbols, ct).ConfigureAwait(false);
            if (HistoryFlags == HistoryFlags.None)
                return snapshots;

            if (historyBase != "")
                await AddCurrenciesToSnapshots(snapshots, historyBase, ct).ConfigureAwait(false);

            if (snapshots.Any(s => s.Value != null && s.Key != (string)s.Value["Symbol"]))
                throw new ArgumentException("Invalid symbol returned.");

            // start history tasks
            foreach (var dict in snapshots.Values.WhereNotNull())
            {
                var symbol = (string)dict["Symbol"];
                if (HistoryFlags.HasFlag(HistoryFlags.PriceHistory))
                {
                    if (!dict.TryGetValue("ExchangeTimezone", out object o))
                        throw new ArgumentException($"No timezone found for symbol: {symbol}.");
                    var tz = (DateTimeZone)o;
                    var closeTime = (LocalTime)dict["ExchangeCloseTime"];
                    dict.Add("PriceHistory", History.GetPricesAsync(symbol, closeTime, tz, ct));
                }
                if (HistoryFlags.HasFlag(HistoryFlags.DividendHistory))
                    dict.Add("DividendHistory", History.GetDividendsAsync(symbol, ct));
                if (HistoryFlags.HasFlag(HistoryFlags.SplitHistory))
                    dict.Add("SplitHistory", History.GetSplitsAsync(symbol, ct));
            }

            // await history tasks
            foreach (var dict in snapshots.Values.WhereNotNull())
            {
                if (dict.TryGetValue("PriceHistory", out dynamic historyTask))
                {
                    dict["PriceHistory"] = await historyTask.ConfigureAwait(false);
                    AppendToPriceHistory(dict);
                }
                if (dict.TryGetValue("DividendHistory", out dynamic dividendTask))
                    dict["DividendHistory"] = await dividendTask.ConfigureAwait(false);
                if (dict.TryGetValue("SplitHistory", out dynamic splitTask))
                    dict["SplitHistory"] = await splitTask.ConfigureAwait(false);
            }

            if (historyBase != "")
                ModifyPriceHistory(symbols, historyBase, snapshots);

            return symbols.ToDictionary(symbol => symbol, symbol => snapshots[symbol]);
        }

        private async Task AddCurrenciesToSnapshots(Dictionary<string, Dictionary<string, object>?> snapshots, string historyBase, CancellationToken ct)
        {
            var rateSymbols = snapshots.Values
                    .WhereNotNull()
                    .Select(d => (string)d["Currency"])
                    .Append(historyBase.EndsWith("=X") ? historyBase.Substring(0, 3) : "")
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Select(c => c.ToUpper())
                    .Where(c => c != "USD")
                    .Select(c => $"USD{c}=X")
                    .Distinct()
                    .Where(c => !snapshots.ContainsKey(c)) // in case already retrieved
                    .ToList();

            if (rateSymbols.Any())
            {
                var currencySnapshots = await Snapshot.GetAsync(rateSymbols, ct).ConfigureAwait(false);
                foreach (var snap in currencySnapshots)
                    snapshots.Add(snap.Key, snap.Value);
            }
        }

        private void AppendToPriceHistory(Dictionary<string, object> snapshot)
        {
            var ticks = (IReadOnlyList<PriceTick>?)snapshot["PriceHistory"];
            if (ticks == null)
                return;
            var date = (ZonedDateTime)snapshot["RegularMarketTime"];
            if (date.ToInstant() <= ticks.Last().Date.ToInstant())
                return;
            snapshot["PriceHistory"] = new List<PriceTick>(ticks)
            {
                new PriceTick(snapshot, 1)
            };
        }

        private void ModifyPriceHistory(List<string> symbols, string historyBase, Dictionary<string, Dictionary<string, object>?> securities)
        {
            IReadOnlyList<PriceTick>? securityBaseTicks = null, currencyBaseTicks = null;
            if (historyBase.EndsWith("=X"))
            {
                if (historyBase != "USD=X")
                {
                    var symbol = "USD" + historyBase;
                    var security = securities[symbol] ?? throw new ArgumentException($"HistoryBase not found: {symbol}.");
                    currencyBaseTicks = (IReadOnlyList<PriceTick>?)security["PriceHistory"] ?? throw new ArgumentException($"HistoryBase PriceHistory not found: {symbol}.");
                }
            }
            else
            {
                var security = securities[historyBase] ?? throw new ArgumentException($"HistoryBase not found: {historyBase}.");
                securityBaseTicks = (IReadOnlyList<PriceTick>)security["PriceHistory"] ?? throw new ArgumentException($"HistoryBase PriceHistory not found: {historyBase}.");
                var currencyBaseSymbol = (string)security["Currency"] ?? throw new ArgumentException("Currency not found.");
                if (currencyBaseSymbol != "USD")
                {
                    var symbol = $"USD{currencyBaseSymbol}=X";
                    var currencyBase = securities[symbol] ?? throw new ArgumentException($"HistoryBase currency not found: {symbol}.");
                    currencyBaseTicks = (IReadOnlyList<PriceTick>)currencyBase["PriceHistory"] ?? throw new ArgumentException($"HistoryBase currency PriceHistory: {symbol}.");
                }
            }

            foreach (var symbol in symbols)
            {
                var security = securities[symbol];
                if (security == null) // unknown symbol
                    continue;
                var ticks = (IReadOnlyList<PriceTick>?)security["PriceHistory"];
                if (ticks == null) // no history
                    continue;

                IReadOnlyList<PriceTick>? currencyTicks = null;
                var currencySymbol = (string)security["Currency"] ?? throw new ArgumentException($"No currency found for: {symbol}.");
                if (currencySymbol != "USD")
                {
                    var sec = securities[$"USD{currencySymbol}=X"];
                    currencyTicks = (IReadOnlyList<PriceTick>?)sec!["PriceHistory"];
                }
                
                var newTicks = new List<PriceTick>(ticks.Count);
                foreach (var tick in ticks)
                {
                    var rate = GetRate(tick.Date, currencyTicks, currencyBaseTicks, securityBaseTicks);
                    if (!double.IsNaN(rate))
                        newTicks.Add(new PriceTick(tick, rate));
                }
                security["PriceHistoryBase"] = newTicks;
            }
        }

        double GetRate(ZonedDateTime date, IReadOnlyList<PriceTick>? currencyRates, IReadOnlyList<PriceTick>? baseCurrencyRates, IReadOnlyList<PriceTick>? baseSecurityRates)
        {
            var instant = date.ToInstant();
            if (currencyRates == baseCurrencyRates)
                currencyRates = baseCurrencyRates = null;

            double rate = 1;

            if (currencyRates != null)
            {
                var r = currencyRates.Interpolate(instant);
                rate /= r;
            }
            if (baseCurrencyRates != null)
            {
                var r = baseCurrencyRates.Interpolate(instant);
                rate *= r;
            }
            if (baseSecurityRates != null)
            {
                var r = baseSecurityRates.Interpolate(instant);
                rate /= r;
            }
            return rate;
        }
    }
}

using Microsoft.Extensions.Logging;
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
            var symbolsChecked = symbols.Select(s => new Symbol(s)).Distinct().ToList();
            if (symbolsChecked.Any(s => s.IsEmpty))
                throw new ArgumentException("Invalid empty symbol.");
            var historyBaseSymbol = new Symbol(historyBase);
            if (historyBaseSymbol.IsEmpty)
            {
                if (symbolsChecked.Any(s => s.IsCurrency))
                    throw new ArgumentException("Currency rate symbols must be in the form ABCEFG=X.");
            }
            else
            {
                if (!HistoryFlags.HasFlag(HistoryFlags.PriceHistory))
                    throw new ArgumentException("PriceHistory must be enabled before specifying historyBase.");
                if (symbolsChecked.Any(s => s.IsCurrencyRate))
                    throw new ArgumentException("Currency symbols must in the form ABC=X.");
                if (historyBaseSymbol.Name == "USD=X" && symbolsChecked.Any(s => s.Name == "USD=X"))
                    throw new ArgumentException("Invalid currency and base symbol: USD=X.");
            }
            var snapshots = await GetAsyncDictionary(symbolsChecked, historyBaseSymbol, ct).ConfigureAwait(false);
            return snapshots.ToDictionary(kvp => kvp.Key, kvp => kvp.Value == null ? null : new Security(kvp.Value), StringComparer.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<string, Dictionary<string, object>?>> GetAsyncDictionary(List<Symbol> symbols, Symbol historyBase, CancellationToken ct)
        {
            var snapshotSymbols = new List<Symbol>(symbols.Where(s => !s.IsCurrency));

            if (!historyBase.IsEmpty && !historyBase.IsCurrency && !snapshotSymbols.Contains(historyBase))
                snapshotSymbols.Add(historyBase);
            var snapshots = await Snapshot.GetAsync(snapshotSymbols
                .Select(x => x.Name).ToList(), ct).ConfigureAwait(false);
            if (HistoryFlags == HistoryFlags.None)
                return snapshots;

            if (!historyBase.IsEmpty)
                await AddCurrenciesToSnapshots(snapshots, symbols, historyBase, ct).ConfigureAwait(false);

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

            if (!historyBase.IsEmpty)
                ModifyPriceHistory(symbols, historyBase, snapshots);

            return symbols.ToDictionary(symbol => symbol.Name, symbol => snapshots[AdjustSymbol(symbol, historyBase).Name]);
        }

        private static Symbol AdjustSymbol(Symbol symbol, Symbol historyBase)
        {
            if (symbol.IsCurrency && historyBase.IsCurrency)
                symbol = new Symbol($"USD{(symbol.Name != "USD=X" ? symbol.Name : historyBase.Name)}");
            return symbol;
        }

        private async Task AddCurrenciesToSnapshots(Dictionary<string, Dictionary<string, object>?> snapshots, List<Symbol> symbols, Symbol historyBase, CancellationToken ct)
        {
            var invalid = snapshots.Values.WhereNotNull().FirstOrDefault(v => !v.ContainsKey("Currency"));
            if (invalid != null)
                throw new ArgumentException($"Currency not found for symbol: {invalid["Symbol"]}.");

            var rateSymbols = snapshots.Values
                    .WhereNotNull()
                    .Select(d => (string)d["Currency"])
                    .Concat(symbols.Where(s => s.IsCurrency).Select(s => s.Currency))
                    .Append(historyBase.IsCurrency ? historyBase.Currency : "")
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
                    snapshots.Add(snap.Key, snap.Value); // long symbol
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

        private static IReadOnlyList<PriceTick> UnifyTicks(IReadOnlyList<PriceTick> oldTicks) =>
            oldTicks.Select(t => new PriceTick(t.Date)).ToList();

        private void ModifyPriceHistory(List<Symbol> symbols, Symbol historyBase, Dictionary<string, Dictionary<string, object>?> securities)
        {
            IReadOnlyList<PriceTick>? securityBaseTicks = null, currencyBaseTicks = null;
            if (historyBase.IsCurrency)
            {
                if (historyBase.Currency != "USD")
                {
                    var symbol = "USD" + historyBase.Name;
                    var security = securities[symbol] ?? throw new ArgumentException($"HistoryBase not found: {symbol}.");
                    currencyBaseTicks = (IReadOnlyList<PriceTick>?)security["PriceHistory"] ?? throw new ArgumentException($"HistoryBase PriceHistory not found: {symbol}.");
                }
            }
            else
            {
                var security = securities[historyBase.Name] ?? throw new ArgumentException($"HistoryBase not found: {historyBase}.");
                securityBaseTicks = (IReadOnlyList<PriceTick>)security["PriceHistory"] ?? throw new ArgumentException($"HistoryBase PriceHistory not found: {historyBase}.");
                var currencyBaseSymbol = (string)security["Currency"] ?? throw new ArgumentException("Currency not found.");
                if (currencyBaseSymbol != "USD")
                {
                    var symbol = $"USD{currencyBaseSymbol}=X";
                    var currencyBase = securities[symbol] ?? throw new ArgumentException($"HistoryBase currency not found: {symbol}.");
                    currencyBaseTicks = (IReadOnlyList<PriceTick>)currencyBase["PriceHistory"] ?? throw new ArgumentException($"HistoryBase currency PriceHistory: {symbol}.");
                }
            }

            foreach (var sym in symbols)
            {
                var symbol = AdjustSymbol(sym, historyBase);
                var security = securities[symbol.Name];
                if (security == null) // unknown symbol
                    continue;
                var ticks = (IReadOnlyList<PriceTick>?)security["PriceHistory"];
                if (ticks == null) // no history
                    continue;

                if (sym.IsCurrency && sym.Currency == "USD")
                    security["PriceHistory"] = ticks = UnifyTicks(ticks);

                IReadOnlyList<PriceTick>? currencyTicks = null;
                if (!sym.IsCurrency) // symbol???
                {
                    var currencySymbol = (string)security["Currency"] ?? throw new ArgumentException($"No currency found for: {symbol.Name}.");
                    if (currencySymbol != "USD")
                    {
                        var sec = securities[$"USD{currencySymbol}=X"];
                        currencyTicks = (IReadOnlyList<PriceTick>?)sec!["PriceHistory"];
                    }
                }

                var newTicks = new List<PriceTick>(ticks.Count);
                foreach (var tick in ticks)
                {
                    var rate = GetRate(tick.Date.ToInstant(), currencyTicks, currencyBaseTicks, securityBaseTicks);
                    if (!double.IsNaN(rate))
                        newTicks.Add(new PriceTick(tick, rate, invert: sym.IsCurrency));
                }
                security["PriceHistoryBase"] = newTicks;
            }
        }

        double GetRate(Instant date, IReadOnlyList<PriceTick>? currencyRates, IReadOnlyList<PriceTick>? baseCurrencyRates, IReadOnlyList<PriceTick>? baseSecurityRates)
        {
            if (currencyRates == baseCurrencyRates)
                currencyRates = baseCurrencyRates = null;

            double rate = 1;

            if (currencyRates != null)
            {
                var r = currencyRates.Interpolate(date);
                rate /= r;
            }
            if (baseCurrencyRates != null)
            {
                var r = baseCurrencyRates.Interpolate(date);
                rate *= r;
            }
            if (baseSecurityRates != null)
            {
                var r = baseSecurityRates.Interpolate(date);
                rate /= r;
            }
            return rate;
        }
    }
}

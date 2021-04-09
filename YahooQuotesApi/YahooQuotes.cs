using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    public sealed class YahooQuotes
    {
        private readonly ILogger Logger;
        private readonly YahooSnapshot Snapshot;
        private readonly YahooHistory History;
        private readonly bool UseNonAdjustedClose;

        internal YahooQuotes(IClock clock, ILogger logger, Duration snapshotCacheDuration, Instant historyStartDate, Frequency frequency, Duration historyCacheDuration, int snapshotDelay, bool nonAdjustedClose)
        {
            Logger = logger;
            var httpFactory = new HttpClientFactoryConfigurator(logger).Produce();
            Snapshot = new YahooSnapshot(clock, logger, httpFactory, snapshotCacheDuration, snapshotDelay);
            History = new YahooHistory(clock, logger, httpFactory, historyStartDate, historyCacheDuration, frequency);
            UseNonAdjustedClose = nonAdjustedClose;
        }

        public async Task<Security?> GetAsync(string symbol, HistoryFlags historyFlags = HistoryFlags.None, string historyBase = "", CancellationToken ct = default) =>
            (await GetAsync(new[] { symbol }, historyFlags, historyBase, ct).ConfigureAwait(false)).Values.Single();

        public async Task<Dictionary<string, Security?>> GetAsync(IEnumerable<string> symbols, HistoryFlags historyFlags = HistoryFlags.None, string historyBase = "", CancellationToken ct = default)
        {
            var historyBaseSymbol = Symbol.TryCreate(historyBase, true) ?? throw new ArgumentException($"Invalid base symbol: {historyBase}.");
            var syms = symbols.ToSymbols().Distinct();
            var securities = await GetAsync(syms, historyFlags, historyBaseSymbol, ct).ConfigureAwait(false);
            return syms.ToDictionary(s => s.Name, s => securities[s], StringComparer.OrdinalIgnoreCase);
        }

        public async Task<Dictionary<Symbol, Security?>> GetAsync(IEnumerable<Symbol> symbols, HistoryFlags historyFlags, Symbol historyBase, CancellationToken ct = default)
        {
            if (symbols.Any(s => s.IsEmpty))
                throw new ArgumentException("Empty symbol.");
            if (historyBase.IsCurrencyRate)
                throw new ArgumentException($"Invalid base symbol: {historyBase}.");
            if (!historyBase.IsEmpty && symbols.Any(s => s.IsCurrencyRate))
                throw new ArgumentException($"Invalid symbol: {symbols.First(s => s.IsCurrencyRate)}.");
            if (historyBase.IsEmpty && symbols.Any(s => s.IsCurrency))
                throw new ArgumentException($"Invalid symbol: {symbols.First(s => s.IsCurrency)}.");
            if (!historyBase.IsEmpty && !historyFlags.HasFlag(HistoryFlags.PriceHistory))
                throw new ArgumentException("PriceHistory must be enabled when historyBase is specified.");
            try
            {
                var securities = await GetSecuritiesyAsync(symbols, historyFlags, historyBase, ct).ConfigureAwait(false);
                return symbols.ToDictionary(symbol => symbol, symbol => securities[symbol]);
            }
            catch (Exception e)
            {
                Logger.LogCritical(e, "YahooQuotes: GetAsync() error.");
                throw;
            }
        }

        private async Task<Dictionary<Symbol, Security?>> GetSecuritiesyAsync(IEnumerable<Symbol> symbols, HistoryFlags historyFlags, Symbol historyBase, CancellationToken ct)
        {
            var stockAndCurrencyRateSymbols = symbols.Where(s => s.IsStock || s.IsCurrencyRate).ToList();
            if (historyBase.IsStock && !stockAndCurrencyRateSymbols.Contains(historyBase))
                stockAndCurrencyRateSymbols.Add(historyBase);
            var securities = await Snapshot.GetAsync(stockAndCurrencyRateSymbols, ct).ConfigureAwait(false);

            if (historyFlags == HistoryFlags.None)
                return securities;

            if (!historyBase.IsEmpty)
                await AddCurrencies(symbols.Where(s => s.IsCurrency), historyBase, securities, ct).ConfigureAwait(false);

            await AddHistoryToSecurities(securities.Values.NotNull(), historyFlags, ct).ConfigureAwait(false);

            if (!historyBase.IsEmpty)
                HistoryBaseComposer.Compose(symbols.ToList(), historyBase, securities);

            return securities;
        }

        private async Task AddCurrencies(IEnumerable<Symbol> currencies, Symbol historyBase, Dictionary<Symbol, Security?> securities, CancellationToken ct)
        {
            // currency securities + historyBase currency + security currencies
            var currencySymbols = new HashSet<Symbol>(currencies);
            if (historyBase.IsCurrency)
                currencySymbols.Add(historyBase);
            foreach (var security in securities.Values.NotNull())
            {
                var currencySymbol = Symbol.TryCreate(security.Currency + "=X");
                if (currencySymbol is null)
                    security.PriceHistoryBase = Result<PriceTick[]>.Fail($"Invalid currency symbol: '{security.Currency}'.");
                else
                currencySymbols.Add(currencySymbol);
            }

            var rateSymbols = currencySymbols
                .Where(c => c.Currency != "USD")
                .Select(c => Symbol.TryCreate($"USD{c.Currency}=X"))
                .NotNull()
                .ToList();

            if (rateSymbols.Any())
            {
                var currencyRateSecurities = await Snapshot.GetAsync(rateSymbols, ct).ConfigureAwait(false);
                foreach (var security in currencyRateSecurities)
                    securities[security.Key] = security.Value; // long symbol
            }
        }

        private async Task AddHistoryToSecurities(IEnumerable<Security> securities, HistoryFlags historyFlags, CancellationToken ct)
        {
            var dividendTasks = new List<(Security, Task<Result<DividendTick[]>>)>();
            if (historyFlags.HasFlag(HistoryFlags.DividendHistory))
                dividendTasks = securities.Select(v => (v, History.GetDividendsAsync(v.Symbol, ct))).ToList();
            var splitTasks = new List<(Security, Task<Result<SplitTick[]>>)>();
            if (historyFlags.HasFlag(HistoryFlags.SplitHistory))
                splitTasks = securities.Select(v => (v, History.GetSplitsAsync(v.Symbol, ct))).ToList();
            var candleTasks = new List<(Security, Task<Result<CandleTick[]>>)>();
            if (historyFlags.HasFlag(HistoryFlags.PriceHistory))
                candleTasks = securities.Select(v => (v, History.GetCandlesAsync(v.Symbol, ct))).ToList();

            foreach (var (security, task) in dividendTasks)
                security.DividendHistory = await task.ConfigureAwait(false);

            foreach (var (security, task) in splitTasks)
                security.SplitHistory = await task.ConfigureAwait(false);
            foreach (var (security, task) in candleTasks)
            {
                var result = await task.ConfigureAwait(false);
                security.PriceHistory = result;
                security.PriceHistoryBase = GetPriceHistoryBaseAsync(result, security);
            }
            return;
        }

        private Result<PriceTick[]> GetPriceHistoryBaseAsync(Result<CandleTick[]> result, Security security)
        {
            if (result.HasError)
                return Result<PriceTick[]>.Fail(result.Error);
            if (security.ExchangeTimezoneName == "")
                return Result<PriceTick[]>.Fail("ExchangeTimezone not found.");
            if (security.ExchangeCloseTime == default)
                return Result<PriceTick[]>.Fail("ExchangeCloseTime not found.");
            return Result<PriceTick[]>.From(() => GetTicks());

            PriceTick[] GetTicks()
            {
                var ticks = result.Value.Select(candleTick =>
                    new PriceTick(candleTick, security.ExchangeCloseTime, security.ExchangeTimezone!, UseNonAdjustedClose)).ToList();
                AppendToPriceHistory(ticks, security);
                return ticks.ToArray();
            }

            void AppendToPriceHistory(List<PriceTick> ticks, Security security)
            {
                if (!ticks.Any())
                    return;
                var historyDate = ticks.Last().Date;
                var snapshotDate = security.RegularMarketTime;
                if (snapshotDate == default)
                {
                    Logger.LogDebug($"RegularMarketTime unavailable for symbol: {security.Symbol}.");
                    return;
                }

                if (snapshotDate.Date < historyDate.Date)
                {
                    Logger.LogDebug($"RegularMarketTime precedes most recent history for symbol: {security.Symbol}.");
                    return;
                }
                if (snapshotDate.Date == historyDate.Date)
                    return;

                var close = security.RegularMarketPrice;
                if (close is null)
                {
                    Logger.LogDebug($"RegularMarketPrice unavailable for symbol: {security.Symbol}.");
                    return;
                }

                var volume = security.RegularMarketVolume;
                if (volume is null )
                {
                    Logger.LogDebug($"RegularMarketVolume unavailable for symbol: {security.Symbol}.");
                    volume = 0;
                }

                ticks.Add(new PriceTick(snapshotDate, Convert.ToDouble(close), volume.Value));
            }
        }
    }
}

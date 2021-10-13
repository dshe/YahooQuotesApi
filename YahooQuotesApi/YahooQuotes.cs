using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    public sealed class YahooQuotes
    {
        private readonly ILogger Logger;
        private readonly IClock Clock;
        private readonly YahooSnapshot Snapshot;
        private readonly YahooHistory History;
        private readonly bool UseNonAdjustedClose;

        internal YahooQuotes(YahooQuotesBuilder builder)
        {
            Logger = builder.Logger;
            Clock = builder.Clock;
            IHttpClientFactory httpFactory = new HttpClientFactoryConfigurator(Logger).Configure();
            Snapshot = new YahooSnapshot(Clock, Logger, httpFactory, builder.SnapshotCacheDuration, builder.UseHttpV2);
            History = new YahooHistory(Clock, Logger, httpFactory, builder.HistoryStartDate, builder.HistoryCacheDuration, builder.HistoryFrequency, builder.UseHttpV2);
            UseNonAdjustedClose = builder.NonAdjustedClose;
        }

        public async Task<Security?> GetAsync(string symbol, HistoryFlags historyFlags = HistoryFlags.None, string? historyBase = null, CancellationToken ct = default) =>
            (await GetAsync(new[] { symbol }, historyFlags, historyBase, ct).ConfigureAwait(false)).Values.Single();

        public async Task<Dictionary<string, Security?>> GetAsync(IEnumerable<string> symbols, HistoryFlags historyFlags = HistoryFlags.None, string? historyBase = null, CancellationToken ct = default)
        {
            List<Symbol> syms = symbols
                .Select(s => Symbol.TryCreate(s) ?? throw new ArgumentException($"Could not convert symbol '{s}' to Symbol."))
                .Distinct()
                .ToList();

            Symbol? historyBaseSymbol = null;
            if (historyBase != null)
            {
                historyBaseSymbol = Symbol.TryCreate(historyBase);
                if (historyBaseSymbol is null)
                    throw new ArgumentException($"Invalid base symbol: {historyBase}.");
            }
            Dictionary<Symbol,Security?> securities = await GetAsync(syms, historyFlags, historyBaseSymbol, ct).ConfigureAwait(false);
            return syms.ToDictionary(s => s.Name, s => securities[s], StringComparer.OrdinalIgnoreCase);
        }

        public async Task<Security?> GetAsync(Symbol symbol, HistoryFlags historyFlags = HistoryFlags.None, Symbol? historyBase = null, CancellationToken ct = default) =>
            (await GetAsync(new[] { symbol }, historyFlags, historyBase, ct).ConfigureAwait(false)).Values.Single();

        public async Task<Dictionary<Symbol, Security?>> GetAsync(IEnumerable<Symbol> symbols, HistoryFlags historyFlags = HistoryFlags.None, Symbol? historyBase = null, CancellationToken ct = default)
        {
            HashSet<Symbol> syms = symbols.ToHashSet();
            if (historyBase is not null && historyBase.IsCurrencyRate)
                throw new ArgumentException($"Invalid base symbol: {historyBase}.");
            if (historyBase is not null && syms.Any(s => s.IsCurrencyRate))
                throw new ArgumentException($"Invalid symbol: {syms.First(s => s.IsCurrencyRate)}.");
            if (historyBase is null && syms.Any(s => s.IsCurrency))
                throw new ArgumentException($"Invalid symbol: {syms.First(s => s.IsCurrency)}.");
            if (historyBase is not null && !historyFlags.HasFlag(HistoryFlags.PriceHistory))
                throw new ArgumentException("PriceHistory must be enabled when historyBase is specified.");
            try
            {
                Dictionary<Symbol, Security?> securities = await GetSecuritiesyAsync(syms, historyFlags, historyBase, ct).ConfigureAwait(false);
                return syms.ToDictionary(symbol => symbol, symbol => securities[symbol]);
            }
            catch (Exception e)
            {
                Logger.LogCritical(e, "YahooQuotes: GetAsync() error.");
                throw;
            }
        }

        private async Task<Dictionary<Symbol, Security?>> GetSecuritiesyAsync(HashSet<Symbol> symbols, HistoryFlags historyFlags, Symbol? historyBase, CancellationToken ct)
        {
            HashSet<Symbol> stockAndCurrencyRateSymbols = symbols.Where(s => s.IsStock || s.IsCurrencyRate).ToHashSet();
            if (historyBase is not null && historyBase.IsStock)
                stockAndCurrencyRateSymbols.Add(historyBase);
            Dictionary<Symbol, Security?> securities = await Snapshot.GetAsync(stockAndCurrencyRateSymbols, ct).ConfigureAwait(false);

            if (historyFlags == HistoryFlags.None)
                return securities;

            if (historyBase is not null)
                await AddCurrencies(symbols, historyBase, securities, ct).ConfigureAwait(false);

            await AddHistoryToSecurities(securities, historyFlags, ct).ConfigureAwait(false);

            if (historyBase is not null)
                HistoryBaseComposer.Compose(symbols, historyBase, securities);

            return securities;
        }

        private async Task AddCurrencies(HashSet<Symbol> symbols, Symbol historyBase, Dictionary<Symbol, Security?> securities, CancellationToken ct)
        {
            // currency securities + historyBase currency + security currencies
            HashSet<Symbol> currencySymbols = symbols.Where(s => s.IsCurrency).ToHashSet();
            if (historyBase.IsCurrency)
                currencySymbols.Add(historyBase);
            foreach (Security security in securities.Values.NotNull())
            {
                if (Symbol.TryCreate(security.Currency + "=X") is Symbol currencySymbol)
                    currencySymbols.Add(currencySymbol);
                else
                    security.PriceHistoryBase = Result<ValueTick[]>.Fail($"Invalid currency symbol: '{security.Currency}'.");
            }

            HashSet<Symbol> rateSymbols = currencySymbols
                .Where(c => c.Currency != "USD")
                .Select(c => Symbol.TryCreate($"USD{c.Currency}=X"))
                .Cast<Symbol>()
                .ToHashSet();

            if (!rateSymbols.Any())
                return;

            Dictionary<Symbol, Security?> currencyRateSecurities = await Snapshot.GetAsync(rateSymbols, ct).ConfigureAwait(false);
            foreach (var security in currencyRateSecurities)
                securities[security.Key] = security.Value; // long symbol
        }

        private async Task AddHistoryToSecurities(Dictionary<Symbol, Security?> securities, HistoryFlags historyFlags, CancellationToken ct)
        {
            List<Security> secs = securities.Values.NotNull().ToList();
            var dividendJobs = secs.Where(_ => historyFlags.HasFlag(HistoryFlags.DividendHistory))
                .Select(sec => (sec, History.GetTicksAsync<DividendTick>(sec.Symbol, ct))).ToList();
            var splitJobs = secs.Where(_ => historyFlags.HasFlag(HistoryFlags.SplitHistory))
                .Select(sec => (sec, History.GetTicksAsync<SplitTick>(sec.Symbol, ct))).ToList();
            var priceJobs = secs.Where(_ => historyFlags.HasFlag(HistoryFlags.PriceHistory))
                .Select(sec => (sec, History.GetTicksAsync<PriceTick>(sec.Symbol, ct))).ToList();

            List<Task> tasks = new();
            tasks.AddRange(dividendJobs.Select(t => t.Item2));
            tasks.AddRange(splitJobs.Select(t => t.Item2));
            tasks.AddRange(priceJobs.Select(t => t.Item2));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var (security, task) in dividendJobs)
                security.DividendHistory = task.Result;
            foreach (var (security, task) in splitJobs)
                security.SplitHistory = task.Result;
            foreach (var (security, task) in priceJobs)
            {
                security.PriceHistory = task.Result;
                security.PriceHistoryBase = GetPriceHistoryBase(task.Result, security);
            }
        }

        private Result<ValueTick[]> GetPriceHistoryBase(Result<PriceTick[]> result, Security security)
        {
            if (result.HasError)
                return Result<ValueTick[]>.Fail(result.Error);
            if (!result.Value.Any())
                return Result<ValueTick[]>.Fail("No history available.");
            if (security.ExchangeTimezone == null)
                return Result<ValueTick[]>.Fail("Exchange timezone not found.");
            if (security.ExchangeCloseTime == default)
                return Result<ValueTick[]>.Fail("ExchangeCloseTime not found.");

			List<ValueTick> ticks = result.Value.Select(priceTick => new ValueTick
            {
                Date = priceTick.Date.At(security.ExchangeCloseTime).InZoneLeniently(security.ExchangeTimezone!).ToInstant(),
                Value = UseNonAdjustedClose ? priceTick.Close : priceTick.AdjustedClose,
                Volume = priceTick.Volume
            }).ToList();

            if (!ticks.Any())
                return Result<ValueTick[]>.Fail("No history available."); // ????????

            return AddLatest(ticks, security);
        }

        private Result<ValueTick[]> AddLatest(List<ValueTick> ticks, Security security)
        {
            ZonedDateTime snapTime = security.RegularMarketTime;
            if (snapTime == default)
            {
                Logger.LogDebug($"RegularMarketTime unavailable for symbol: {security.Symbol}.");
                return Result<ValueTick[]>.Ok(ticks.ToArray());
            }

            decimal? snapPrice = security.RegularMarketPrice;
            if (snapPrice is null)
            {
                Logger.LogDebug($"RegularMarketPrice unavailable for symbol: {security.Symbol}.");
                return Result<ValueTick[]>.Ok(ticks.ToArray());
            }

            Instant now = Clock.GetCurrentInstant();
            Instant snapTimeInstant = snapTime.ToInstant();
            if (snapTimeInstant > now)
            {
                if ((snapTimeInstant - now) > Duration.FromSeconds(10))
                    Logger.LogWarning($"Snapshot date: {snapTimeInstant} which follows current date: {now} adjusted for symbol: {security.Symbol}.");
                snapTimeInstant = now;
            }

            ValueTick latestHistory = ticks.Last();
            if (latestHistory.Date >= snapTimeInstant)
            {   // if history already includes snapshot, or exchange closes early
                Logger.LogTrace($"History tick with date: {latestHistory.Date} follows snapshot date: {snapTimeInstant} removed for symbol: {security.Symbol}.");
                ticks.Remove(latestHistory);
                if (!ticks.Any() || ticks.Last().Date >= snapTimeInstant)
                    return Result<ValueTick[]>.Fail($"Invalid dates.");
            }

            long? volume = security.RegularMarketVolume;
            if (volume is null)
            {
                Logger.LogTrace($"RegularMarketVolume unavailable for symbol: {security.Symbol}.");
                volume = 0;
            }

            ticks.Add(new ValueTick { 
                Date = snapTimeInstant,
                Value = Convert.ToDouble(snapPrice),
                Volume = volume.Value }); // hist < snap < now
            return Result<ValueTick[]>.Ok(ticks.ToArray());
        }
    }
}

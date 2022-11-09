using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi;

public sealed class YahooQuotes : IDisposable
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
        IHttpClientFactory httpFactory = new HttpClientFactoryCreator(Logger).Create();
        Snapshot = new YahooSnapshot(Clock, Logger, httpFactory, builder.SnapshotCacheDuration);
        History = new YahooHistory(Clock, Logger, httpFactory, builder.HistoryStartDate, builder.HistoryCacheDuration, builder.PriceHistoryFrequency);
        UseNonAdjustedClose = builder.NonAdjustedClose;
    }

    public async Task<Security?> GetAsync(string symbol, Histories historyFlags = Histories.None, string historyBase = "", CancellationToken ct = default) =>
        (await GetAsync(new[] { symbol }, historyFlags, historyBase, ct).ConfigureAwait(false)).Values.Single();

    public async Task<Dictionary<string, Security?>> GetAsync(IEnumerable<string> symbols, Histories historyFlags = Histories.None, string historyBase = "", CancellationToken ct = default)
    {
        List<Symbol> syms = symbols
            .Select(s => s.ToSymbol())
            .Distinct()
            .ToList();

        Symbol? historyBaseSymbol = null;
        if (!string.IsNullOrEmpty(historyBase))
        {
            if (!Symbol.TryCreate(historyBase, out Symbol hbs))
                throw new ArgumentException($"Invalid base symbol: {historyBase}.");
            historyBaseSymbol = hbs;
        }
        Dictionary<Symbol, Security?> securities = await GetAsync(syms, historyFlags, historyBaseSymbol, ct).ConfigureAwait(false);
        return syms.ToDictionary(s => s.Name, s => securities[s], StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Security?> GetAsync(Symbol symbol, Histories historyFlags = Histories.None, Symbol? historyBase = null, CancellationToken ct = default) =>
        (await GetAsync(new[] { symbol }, historyFlags, historyBase, ct).ConfigureAwait(false)).Values.Single();

    public async Task<Dictionary<Symbol, Security?>> GetAsync(IEnumerable<Symbol> symbols, Histories historyFlags = Histories.None, Symbol? historyBase = null, CancellationToken ct = default)
    {
        HashSet<Symbol> syms = symbols.ToHashSet();
        if (historyBase is not null)
        {
            if (historyBase.Value.IsCurrencyRate)
                throw new ArgumentException($"Invalid base symbol: {historyBase}.");
            if (syms.Any(s => s.IsCurrencyRate))
                throw new ArgumentException($"Invalid symbol: {syms.First(s => s.IsCurrencyRate)}.");
            if (!historyFlags.HasFlag(Histories.PriceHistory))
                throw new ArgumentException("PriceHistory must be enabled when historyBase is specified.");
        }
        if (historyBase is null && syms.Any(s => s.IsCurrency))
            throw new ArgumentException($"Invalid symbol: {syms.First(s => s.IsCurrency)}.");
        try
        {
            Dictionary<Symbol, Security?> securities = await GetSecuritiesAsync(syms, historyFlags, historyBase, ct).ConfigureAwait(false);
            return syms.ToDictionary(symbol => symbol, symbol => securities[symbol]);
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "YahooQuotes: GetAsync() error.");
            throw;
        }
    }

    private async Task<Dictionary<Symbol, Security?>> GetSecuritiesAsync(HashSet<Symbol> symbols, Histories historyFlags, Symbol? historyBase, CancellationToken ct)
    {
        HashSet<Symbol> stockAndCurrencyRateSymbols = symbols.Where(s => s.IsStock || s.IsCurrencyRate).ToHashSet();
        if (historyBase is not null && historyBase.Value.IsStock)
            stockAndCurrencyRateSymbols.Add(historyBase.Value);
        Dictionary<Symbol, Security?> securities = await Snapshot.GetAsync(stockAndCurrencyRateSymbols, ct).ConfigureAwait(false);

        if (historyFlags == Histories.None)
            return securities;

        if (historyBase is not null)
            await AddCurrencies(symbols, historyBase.Value, securities, ct).ConfigureAwait(false);

        await AddHistoryToSecurities(securities, historyFlags, ct).ConfigureAwait(false);

        if (historyBase is not null)
            HistoryBaseComposer.Compose(symbols, historyBase.Value, securities);

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
            if (!Symbol.TryCreate(security.Currency + "=X", out Symbol currencySymbol))
                security.PriceHistoryBase = Result<ValueTick[]>.Fail($"Invalid currency symbol: '{security.Currency}'.");
            else
                currencySymbols.Add(currencySymbol);
        }

        HashSet<Symbol> rateSymbols = currencySymbols
            .Where(c => c.Currency is not "USD")
            .Select(c => $"USD{c.Currency}=X".ToSymbol())
            .ToHashSet();

        if (!rateSymbols.Any())
            return;

        Dictionary<Symbol, Security?> currencyRateSecurities = await Snapshot.GetAsync(rateSymbols, ct).ConfigureAwait(false);
        foreach (var security in currencyRateSecurities)
            securities[security.Key] = security.Value; // long symbol
    }

    private async Task AddHistoryToSecurities(Dictionary<Symbol, Security?> securities, Histories historyFlags, CancellationToken ct)
    {
        Histories[] histories = Enum.GetValues<Histories>().Where(history => historyFlags.HasFlag(history)).ToArray();

        (Security security, Histories flag)[] jobs = securities.Values
            .NotNull()
            .Select(security => histories.Select(history => (security, history)))
            .SelectMany(x => x)
            .ToArray();

        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = 8,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(jobs, parallelOptions, async (job, ct) =>
        {
            Security security = job.security;
            Histories flag = job.flag;
            if (flag == Histories.PriceHistory)
            {
                security.PriceHistory = await History.GetTicksAsync<PriceTick>(security.Symbol, ct).ConfigureAwait(false);
                security.PriceHistoryBase = GetPriceHistoryBase(security.PriceHistory, security);
            }
            else if (flag == Histories.DividendHistory)
                security.DividendHistory = await History.GetTicksAsync<DividendTick>(security.Symbol, ct).ConfigureAwait(false);
            else if (flag == Histories.SplitHistory)
                security.SplitHistory = await History.GetTicksAsync<SplitTick>(security.Symbol, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private Result<ValueTick[]> GetPriceHistoryBase(Result<PriceTick[]> result, Security security)
    {
        if (result.HasError)
            return Result<ValueTick[]>.Fail(result.Error);
        if (!result.Value.Any())
            return Result<ValueTick[]>.Fail("No history available.");
        if (security.ExchangeTimezone is null)
            return Result<ValueTick[]>.Fail("ExchangeTimezone not found.");
        if (security.ExchangeCloseTime == default)
            return Result<ValueTick[]>.Fail("ExchangeCloseTime not found.");

        List<ValueTick> ticks = result.Value.Select(priceTick => new ValueTick(
            priceTick.Date.At(security.ExchangeCloseTime).InZoneLeniently(security.ExchangeTimezone!).ToInstant(),
            UseNonAdjustedClose ? priceTick.Close : priceTick.AdjustedClose,
            priceTick.Volume
        )).ToList();

        return AddLatest(ticks, security);
    }

    private Result<ValueTick[]> AddLatest(List<ValueTick> ticks, Security security)
    {
        if (security.RegularMarketPrice is null)
        {
            Logger.LogDebug("RegularMarketPrice unavailable for symbol: {Symbol}.", security.Symbol);
            return Result<ValueTick[]>.Ok(ticks.ToArray());
        }

        if (security.RegularMarketTime == default)
        {
            Logger.LogDebug("RegularMarketTime unavailable for symbol: {Symbol}.", security.Symbol);
            return Result<ValueTick[]>.Ok(ticks.ToArray());
        }

        Instant now = Clock.GetCurrentInstant();
        Instant snapTime = security.RegularMarketTime.ToInstant();
        if (snapTime > now)
        {
            if ((snapTime - now) > Duration.FromSeconds(20))
                Logger.LogWarning("Snapshot date: {SnapTimeInstant} which follows current date: {Now} adjusted for symbol: {Symbol}.", snapTime, now, security.Symbol);
            snapTime = now;
        }

        // assume snaptime is correct
        while (ticks.Any())
        {
            ValueTick lastHistory = ticks.Last()!;
            Instant lastDate = lastHistory.Date;
            if (snapTime > lastDate)
                break;
            // if history already includes snapshot, or exchange closes early
            // hist < snap <= now
            Logger.LogTrace("History tick with date: {Date} equals or follows snapshot date: {SnapTimeInstant} removed for symbol: {Symbol}.", lastDate, snapTime, security.Symbol);
            ticks.Remove(lastHistory);
        }

        ticks.Add(new ValueTick(
            snapTime,
            Convert.ToDouble(security.RegularMarketPrice.Value, CultureInfo.InvariantCulture),
            security.RegularMarketVolume ?? 0
        )); 

        return Result<ValueTick[]>.Ok(ticks.ToArray());
    }

    public void Dispose() => Snapshot.Dispose();
}

using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi;

public sealed partial class YahooQuotes
{
    private readonly ILogger Logger;
    private readonly YahooSnapshot Snapshot;
    private readonly YahooHistory History;
    private readonly HistoryBaseComposer HistoryBaseComposer;
    private readonly YahooModules Modules;

    // must be public to support dependency injection
    public YahooQuotes(YahooQuotesBuilder builder, YahooSnapshot snapshot, YahooHistory history, YahooModules modules, HistoryBaseComposer hbc)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        Logger = builder.Logger;
        Snapshot = snapshot;
        History = history;
        HistoryBaseComposer = hbc;
        Modules = modules;
    }

    public async Task<Security?> GetAsync(string symbol, Histories historyFlags = default, string historyBase = "", CancellationToken ct = default) =>
        (await GetAsync(new[] { symbol }, historyFlags, historyBase, ct).ConfigureAwait(false)).Values.Single();

    public async Task<Security?> GetAsync(Symbol symbol, Histories historyFlags = default, Symbol historyBase = default, CancellationToken ct = default) =>
        (await GetAsync(new[] { symbol }, historyFlags, historyBase, ct).ConfigureAwait(false)).Values.Single();

    public async Task<Dictionary<string, Security?>> GetAsync(IEnumerable<string> symbols, Histories historyFlags = default, string historyBase = "", CancellationToken ct = default)
    {
        HashSet<Symbol> syms = symbols
            .Select(s => s.ToSymbol())
            .ToHashSet();

        Symbol historyBaseSymbol = default;
        if (!string.IsNullOrEmpty(historyBase))
        {
            if (!Symbol.TryCreate(historyBase, out Symbol hbs))
                throw new ArgumentException($"Invalid base symbol: {historyBase}.");
            historyBaseSymbol = hbs;
        }
        Dictionary<Symbol, Security?> securities = await GetSecuritiesAsync1(syms, historyFlags, historyBaseSymbol, ct).ConfigureAwait(false);
        return syms.ToDictionary(s => s.Name, s => securities[s], StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<Symbol, Security?>> GetAsync(IEnumerable<Symbol> symbols, Histories historyFlags = default, Symbol historyBase = default, CancellationToken ct = default)
    {
        HashSet<Symbol> syms = symbols.ToHashSet();
        Dictionary<Symbol, Security?> securities = await GetSecuritiesAsync1(syms, historyFlags, historyBase, ct).ConfigureAwait(false);
        return syms.ToDictionary(s => s, s => securities[s]);
    }

    private async Task<Dictionary<Symbol, Security?>> GetSecuritiesAsync1(HashSet<Symbol> symbols, Histories historyFlags, Symbol historyBase, CancellationToken ct)
    {
        if (historyBase == default && symbols.Any(s => s.IsCurrency))
            throw new ArgumentException($"Invalid symbol: {symbols.First(s => s.IsCurrency)}.");
        if (historyBase != default)
        {
            if (!historyFlags.HasFlag(Histories.PriceHistory))
                throw new ArgumentException("PriceHistory must be enabled when historyBase is specified.");
            if (historyBase.IsCurrencyRate)
                throw new ArgumentException($"Invalid base symbol: {historyBase}.");
            if (symbols.Any(s => s.IsCurrencyRate))
                throw new ArgumentException($"Invalid symbol: {symbols.First(s => s.IsCurrencyRate)}.");
        }

        try
        {
            return await GetSecuritiesAsync2(symbols, historyFlags, historyBase, ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "YahooQuotes GetAsync() error.");
            throw;
        }
    }

    private async Task<Dictionary<Symbol, Security?>> GetSecuritiesAsync2(HashSet<Symbol> symbols, Histories historyFlags, Symbol historyBase, CancellationToken ct)
    {
        HashSet<Symbol> stockAndCurrencyRateSymbols = symbols.Where(s => s.IsStock || s.IsCurrencyRate).ToHashSet();
        if (historyBase != default && historyBase.IsStock)
            stockAndCurrencyRateSymbols.Add(historyBase);
        Dictionary<Symbol, Security?> securities = await Snapshot.GetAsync(stockAndCurrencyRateSymbols, ct).ConfigureAwait(false);

        if (historyFlags == Histories.None)
            return securities;

        if (historyBase != default)
            await AddCurrenciesToSecurities(symbols, historyBase, securities, ct).ConfigureAwait(false);

        await AddHistoryToSecurities(securities, historyFlags, ct).ConfigureAwait(false);

        if (historyFlags.HasFlag(Histories.PriceHistory))
            HistoryBaseComposer.Compose(symbols, historyBase, securities);

        return securities;
    }

    private async Task AddCurrenciesToSecurities(HashSet<Symbol> symbols, Symbol historyBase, Dictionary<Symbol, Security?> securities, CancellationToken ct)
    {
        // currency securities + historyBase currency + security currencies
        HashSet<Symbol> currencySymbols = symbols.Where(s => s.IsCurrency).ToHashSet();
        foreach (Security security in securities.Values.NotNull())
        {
            if (Symbol.TryCreate(security.Currency + "=X", out Symbol currencySymbol))
                currencySymbols.Add(currencySymbol);
            else
                security.PriceHistoryBase = Result<ValueTick[]>.Fail($"Invalid currency symbol: '{security.Currency}'.");
        }
        if (historyBase.IsCurrency)
            currencySymbols.Add(historyBase);

        HashSet<Symbol> rateSymbols = currencySymbols
            .Where(c => c.Currency is not "USD")
            .Select(c => $"USD{c.Currency}=X".ToSymbol())
            .ToHashSet();

        if (!rateSymbols.Any())
            return;

        Dictionary<Symbol, Security?> currencyRateSecurities = await Snapshot.GetAsync(rateSymbols, ct).ConfigureAwait(false);
        foreach (KeyValuePair<Symbol, Security?> kvp in currencyRateSecurities)
            securities[kvp.Key] = kvp.Value;
    }

    private async Task AddHistoryToSecurities(Dictionary<Symbol, Security?> securities, Histories historyFlags, CancellationToken ct)
    {
        Histories[] histories = Enum.GetValues<Histories>()
            .Where(history => historyFlags.HasFlag(history))
            .ToArray();

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
            Histories flag = job.flag;
            Security security = job.security;
            Symbol symbol = security.Symbol;
            if (flag is Histories.PriceHistory)
                security.PriceHistory = await History.GetTicksAsync<PriceTick>(symbol, ct).ConfigureAwait(false);
            else if (flag is Histories.DividendHistory)
                security.DividendHistory = await History.GetTicksAsync<DividendTick>(symbol, ct).ConfigureAwait(false);
            else if (flag is Histories.SplitHistory)
                security.SplitHistory = await History.GetTicksAsync<SplitTick>(symbol, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    //////////////////////////////

    public async Task<Result<JsonProperty>> GetModuleAsync(string symbol, string module, CancellationToken ct = default)
    {
        Result<JsonProperty[]> result = await GetModulesAsync(symbol, new[] { module }, ct).ConfigureAwait(false);
        return result.ToResult(v => v.Single());
    }

    public async Task<Result<JsonProperty[]>> GetModulesAsync(string symbol, IEnumerable<string> modules, CancellationToken ct = default)
    {
        try
        {
            Result<JsonProperty[]> result = await Modules.GetModulesAsync(symbol, modules.ToArray(), ct).ConfigureAwait(false);
            if (result.HasError)
                Logger.LogWarning("YahooQuotes:GetModulesAsync() error: {Message}.", result.Error);
            return result;
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "YahooQuotes GetModulesAsync() error.");
            throw;
        }
    }
}

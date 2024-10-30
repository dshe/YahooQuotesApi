using System.Text.Json;
namespace YahooQuotesApi;

public sealed class YahooQuotes(ILogger logger, CookieAndCrumb cookieAndCrumb, YahooSnapshot snapshot, YahooHistory history,  YahooModules modules)
{
    private ILogger Logger { get; } = logger;
    private CookieAndCrumb CookieAndCrumb { get; } = cookieAndCrumb;
    private YahooSnapshot Snapshot { get; } = snapshot;
    private YahooHistory History { get; } = history;
    private YahooModules Modules { get; } = modules;


    public async Task<Snapshot?> GetSnapshotAsync(string symbol, CancellationToken ct = default) =>
        await GetSnapshotAsync(symbol.ToSymbol(), ct).ConfigureAwait(false);

    public async Task<Dictionary<string, Snapshot?>> GetSnapshotAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        Dictionary<Symbol, Snapshot?> results = await GetSnapshotAsync(symbols.Select(s => s.ToSymbol()), ct).ConfigureAwait(false);
        return results.ToDictionary(s => s.Key.Name, static s => s.Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Snapshot?> GetSnapshotAsync(Symbol symbol, CancellationToken ct = default) =>
        (await GetSnapshotAsync([symbol], ct).ConfigureAwait(false)).Values.Single();
    
    public async Task<Dictionary<Symbol, Snapshot?>> GetSnapshotAsync(IEnumerable<Symbol> symbols, CancellationToken ct = default) =>
        await Snapshot.GetAsync(symbols, ct).ConfigureAwait(false);



    public async Task<Result<History>> GetHistoryAsync(string symbol, string baseSymbol = "", CancellationToken ct = default) =>
        (await GetHistoryAsync([symbol], baseSymbol, ct).ConfigureAwait(false)).Values.Single();

    public async Task<Dictionary<string, Result<History>>> GetHistoryAsync(IEnumerable<string> symbols, string baseSymbol = "", CancellationToken ct = default)
    {
        Symbol baseSym = string.IsNullOrEmpty(baseSymbol) ? default : baseSymbol.ToSymbol();
        Dictionary<Symbol, Result<History>> results = await GetHistoryAsync(symbols.Select(s => s.ToSymbol()), baseSym, ct).ConfigureAwait(false);
        return results.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
    }

    public async Task<Result<History>> GetHistoryAsync(Symbol symbol, Symbol baseSymbol = default, CancellationToken ct = default) =>
        (await GetHistoryAsync([symbol], baseSymbol, ct).ConfigureAwait(false)).Values.Single();

    public async Task<Dictionary<Symbol, Result<History>>> GetHistoryAsync(IEnumerable<Symbol> symbols, Symbol baseSymbol = default, CancellationToken ct = default) =>
        await History.GettHistoryAsync(symbols, baseSymbol, ct).ConfigureAwait(false);



    public async Task<Result<JsonProperty>> GetModuleAsync(string symbol, string module, CancellationToken ct = default)
    {
        Result<JsonProperty[]> result = await GetModulesAsync(symbol, [module], ct).ConfigureAwait(false);
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

    // testing
    internal async Task<(string[], string)> GetCookieAndCrumbAsync() =>
          await CookieAndCrumb.Get(default).ConfigureAwait(false);
}

using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi;

public sealed class YahooQuotes
{
    private readonly ILogger Logger;
    private readonly Quotes Quotes;
    private readonly YahooModules Modules;

    // must be public to support dependency injection
    public YahooQuotes(YahooQuotesBuilder builder, YahooModules modules, Quotes quotes)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        Logger = builder.Logger;
        Quotes = quotes;
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
        Dictionary<Symbol, Security?> securities = await Quotes.GetAsync(syms, historyFlags, historyBaseSymbol, ct).ConfigureAwait(false);
        return syms.ToDictionary(s => s.Name, s => securities[s], StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<Symbol, Security?>> GetAsync(IEnumerable<Symbol> symbols, Histories historyFlags = default, Symbol historyBase = default, CancellationToken ct = default)
    {
        HashSet<Symbol> syms = symbols.ToHashSet();
        Dictionary<Symbol, Security?> securities = await Quotes.GetAsync(syms, historyFlags, historyBase, ct).ConfigureAwait(false);
        return syms.ToDictionary(s => s, s => securities[s]);
    }

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

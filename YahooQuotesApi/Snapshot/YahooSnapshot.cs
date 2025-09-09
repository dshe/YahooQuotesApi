using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
namespace YahooQuotesApi;

public sealed class YahooSnapshot : IDisposable
{
    private ILogger Logger { get; }
    private IHttpClientFactory HttpClientFactory { get; }
    private SnapshotCreator SnapshotCreator { get; }
    private CookieAndCrumb CookieAndCrumb { get; }
    private SerialProducerCache<Symbol, Snapshot?> Cache { get; }
 
    public YahooSnapshot(ILogger logger, YahooQuotesBuilder builder, CookieAndCrumb crumbService, SnapshotCreator sc, IHttpClientFactory factory)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        Logger = logger;
        CookieAndCrumb = crumbService;
        HttpClientFactory = factory;
        SnapshotCreator = sc;
        Cache = new SerialProducerCache<Symbol, Snapshot?>(builder.Clock, builder.SnapshotCacheDuration, Producer);
    }

    internal async Task<Dictionary<Symbol, Snapshot?>> GetAsync(IEnumerable<Symbol> syms, CancellationToken ct)
    {
        HashSet<Symbol> symbols = [.. syms];
        if (symbols.Count == 0)
            return [];
        if (symbols.Any(s => s.IsCurrency))
            throw new ArgumentException($"Invalid symbol: {symbols.First(s => s.IsCurrency)}.");
        try
        {
            return await Cache.Get(symbols, ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "YahooQuotes GetAsync() error.");
            throw;
        }
    }

    private async Task<Dictionary<Symbol, Snapshot?>> Producer(List<Symbol> symbols, CancellationToken ct)
    {
        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct
        };

        (string[] cookies, string crumb) = await CookieAndCrumb.Get(ct).ConfigureAwait(false);

        IEnumerable<Uri> uris = GetUris(symbols, crumb);

        // Unknown symbols are ignored
        Dictionary<Symbol, Snapshot?> snapshots = symbols.ToDictionary(s => s, s => (Snapshot?)null);

        await Parallel.ForEachAsync(uris, parallelOptions, async (uri, ct) =>
        {
            List<Snapshot> someSnapshots = await MakeRequest(uri, cookies, ct).ConfigureAwait(false);
            lock (snapshots)
            {
                foreach (var snapshot in someSnapshots)
                    snapshots[snapshot.Symbol] = snapshot;
                
            }
        }).ConfigureAwait(false);

        return snapshots;
    }

    private static IEnumerable<Uri> GetUris(IEnumerable<Symbol> symbols, string crumb)
    {
        string baseUrl = $"https://query2.finance.yahoo.com/v7/finance/quote?symbols=";
        return symbols
            .Select(symbol => WebUtility.UrlEncode(symbol.Name))
            .Chunk(100)
            .Select(s => $"{baseUrl}{string.Join(",", s)}&crumb={crumb}")
            .Select(s => new Uri(s));
    }

    private async Task<List<Snapshot>> MakeRequest(Uri uri, string[] cookies, CancellationToken ct)
    {
        Logger.LogInformation("{Uri}", uri.ToString());

        HttpClient httpClient = HttpClientFactory.CreateClient("HttpV2");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("Cookie", cookies);

        using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType != "application/json")
        {
            response.EnsureSuccessStatusCode();
            throw new InvalidOperationException($"Unexpected media type: {mediaType}");
        }

        using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using JsonDocument jsonDocument = await JsonDocument.ParseAsync(stream, default, ct).ConfigureAwait(false);
        return SnapshotCreator.CreateFromJson(jsonDocument);
    }

    public void Dispose() => Cache.Dispose();
}
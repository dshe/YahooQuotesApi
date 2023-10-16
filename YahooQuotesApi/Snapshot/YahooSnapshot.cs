using System.Data;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using YahooQuotesApi.Crumb;

namespace YahooQuotesApi;

public sealed class YahooSnapshot : IDisposable
{
    private readonly ILogger Logger;
    private readonly IHttpClientFactory HttpClientFactory;
    private readonly string ApiVersion;
    private readonly YahooCrumb YahooCrumbService;
    private readonly SerialProducerCache<Symbol, Security?> Cache;
    private readonly string SpecificUserAgent;

    public YahooSnapshot(IClock clock, ILogger logger, YahooQuotesBuilder builder, YahooCrumb crumbService, IHttpClientFactory factory)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        Logger = logger;
        HttpClientFactory = factory;
        ApiVersion = builder.SnapshotApiVersion;
        YahooCrumbService = crumbService;
        Cache = new SerialProducerCache<Symbol, Security?>(clock, builder.SnapshotCacheDuration, Producer);
        SpecificUserAgent = builder.SpecificUserAgent;
    }

    internal async Task<Dictionary<Symbol, Security?>> GetAsync(HashSet<Symbol> symbols, CancellationToken ct)
    {
        Symbol currency = symbols.FirstOrDefault(s => s.IsCurrency);
        if (currency.IsValid)
            throw new ArgumentException($"Invalid symbol: {currency} (currency).");

        return await Cache.Get(symbols, ct).ConfigureAwait(false);
    }

    private async Task<Dictionary<Symbol, Security?>> Producer(Symbol[] symbols, CancellationToken ct)
    {
        Dictionary<Symbol, Security?> dict = symbols.ToDictionary(s => s, s => (Security?)null);

        if (!symbols.Any())
            return dict;

        IEnumerable<JsonElement> elements = await GetElements(symbols, ct).ConfigureAwait(false);

        foreach (JsonElement element in elements)
        {
            Security security = new(element, Logger);
            Symbol symbol = security.Symbol;

            if (!dict.ContainsKey(symbol))
                throw new InvalidOperationException(symbol.Name);

            dict[symbol] = security;
        }

        return dict;
    }

    private async Task<IEnumerable<JsonElement>> GetElements(Symbol[] symbols, CancellationToken ct)
    {
        var (cookieValue, crumb) = await YahooCrumbService.GetCookieAndCrumb(ct).ConfigureAwait(false);

        const string format = "https://query2.finance.yahoo.com/{0}/finance/quote?symbols=";
        string baseUrl = string.Format(CultureInfo.InvariantCulture, format, ApiVersion);

        (Uri uri, List<JsonElement> elements)[] datas =
            GetUris(baseUrl, symbols, crumb)
                .Select(uri => (uri, elements: new List<JsonElement>()))
                .ToArray();

        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(datas, parallelOptions, async (data, ct) =>
            data.elements.AddRange(await MakeRequest(data.uri, cookieValue, ct).ConfigureAwait(false))).ConfigureAwait(false);

        return datas.Select(x => x.elements).SelectMany(x => x);
    }

    private static IEnumerable<Uri> GetUris(string baseUrl, IEnumerable<Symbol> symbols, string crumb)
    {
        return symbols
            .Select(symbol => WebUtility.UrlEncode(symbol.Name))
            .Chunk(100)
            .Select(s => $"{baseUrl}{string.Join(",", s)}&crumb={crumb}")
            .Select(s => new Uri(s));
    }

    private async Task<JsonElement[]> MakeRequest(Uri uri, IEnumerable<string> cookieValue, CancellationToken ct)
    {
        Logger.LogInformation("{Uri}", uri.ToString());

        HttpClient httpClient = HttpClientFactory.CreateClient("snapshot");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("Cookie", cookieValue);
        if (!string.IsNullOrEmpty(SpecificUserAgent))
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(SpecificUserAgent);

        //Don't use GetFromJsonAsync() or GetStreamAsync() because it would throw an exception
        //and not allow reading a json error messages such as NotFound.
        using HttpResponseMessage response = await httpClient.GetAsync(uri, ct).ConfigureAwait(false);
        using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        JsonDocument jsonDocument = await JsonDocument.ParseAsync(stream, default, ct).ConfigureAwait(false);

        if (!jsonDocument.RootElement.TryGetProperty("quoteResponse", out JsonElement quoteResponse))
            throw new InvalidDataException("quoteResponse");

        if (!quoteResponse.TryGetProperty("error", out JsonElement error))
            throw new InvalidDataException("error");

        if (error.ValueKind is not JsonValueKind.Null)
        {
            string errorMessage = error.ToString();
            if (error.TryGetProperty("description", out JsonElement property))
            {
                string? description = property.GetString();
                if (description is not null)
                    errorMessage = description;
            }
            throw new InvalidDataException($"Error requesting YahooSnapshot: {errorMessage}");
        }

        if (!quoteResponse.TryGetProperty("result", out JsonElement result))
            throw new InvalidDataException("result");

        return result.EnumerateArray().ToArray();
    }

    public void Dispose() => Cache.Dispose();
}
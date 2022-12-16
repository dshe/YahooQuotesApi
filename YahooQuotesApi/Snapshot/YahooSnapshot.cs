using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi;

public sealed class YahooSnapshot : IDisposable
{
    private readonly ILogger Logger;
    private readonly IHttpClientFactory HttpClientFactory;
    private readonly SerialProducerCache<Symbol, Security?> Cache;

    public YahooSnapshot(YahooQuotesBuilder builder, IHttpClientFactory factory)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        Logger = builder.Logger;
        HttpClientFactory = factory;
        Cache = new SerialProducerCache<Symbol, Security?>(builder.Clock, builder.SnapshotCacheDuration, Producer);
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
        (Uri uri, List<JsonElement> elements)[] datas =
            GetUris(symbols)
                .Select(uri => (uri, elements: new List<JsonElement>()))
                .ToArray();

        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(datas, parallelOptions, async (data, ct) =>
            data.elements.AddRange(await MakeRequest(data.uri, ct).ConfigureAwait(false))).ConfigureAwait(false);

        return datas.Select(x => x.elements).SelectMany(x => x);
    }

    private static IEnumerable<Uri> GetUris(IEnumerable<Symbol> symbols)
    {
        const string baseUrl = "https://query2.finance.yahoo.com/v7/finance/quote?symbols=";

        return symbols
            .Select(symbol => WebUtility.UrlEncode(symbol.Name))
            .Chunk(100)
            .Select(s => $"{baseUrl}{string.Join(",", s)}")
            .Select(s => new Uri(s));
    }

    private async Task<JsonElement[]> MakeRequest(Uri uri, CancellationToken ct)
    {
        Logger.LogInformation("{Uri}", uri.ToString());

        HttpClient httpClient = HttpClientFactory.CreateClient("snapshot");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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

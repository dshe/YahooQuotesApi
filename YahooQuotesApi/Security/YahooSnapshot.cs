using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Invalid symbols are ignored by Yahoo.

namespace YahooQuotesApi
{
    internal class YahooSnapshot
    {
        private readonly ILogger Logger;
        private readonly HttpClient HttpClient;
        private readonly SerialProducerCache<Symbol, Security?> Cache;
        private readonly bool UseHttpV2;

        internal YahooSnapshot(IClock clock, ILogger logger, IHttpClientFactory factory, Duration cacheDuration, bool useHttpV2)
        {
            Logger = logger;
            HttpClient = factory.CreateClient("snapshot");
            Cache = new SerialProducerCache<Symbol, Security?>(clock, cacheDuration, Producer);
            UseHttpV2 = useHttpV2;
        }

        internal async Task<Dictionary<Symbol, Security?>> GetAsync(HashSet<Symbol> symbols, CancellationToken ct = default)
        {
            Symbol? currency = symbols.FirstOrDefault(s => s.IsCurrency);
            if (currency != default)
                throw new ArgumentException($"Invalid symbol: {currency} (currency).");

            return await Cache.Get(symbols, ct).ConfigureAwait(false);
        }
        private async Task<Dictionary<Symbol, Security?>> Producer(HashSet<Symbol> symbols, CancellationToken ct)
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

        private async Task<IEnumerable<JsonElement>> GetElements(HashSet<Symbol> symbols, CancellationToken ct)
        {
            // start tasks
            var tasks = GetUris(symbols).Select(u => MakeRequest(u, ct));
            var responses = await TaskExt.WhenAll(tasks).ConfigureAwait(false);
            return responses.SelectMany(x => x).ToList();
        }

        private static IEnumerable<Uri> GetUris(HashSet<Symbol> symbols)
        {
            const string baseUrl = "https://query2.finance.yahoo.com/v7/finance/quote";

            return PartitionSymbols(symbols)
                .Select(s => $"{baseUrl}?symbols={string.Join(",", s)}")
                .Select(s => new Uri(s));
        }

        private static List<List<string>> PartitionSymbols(HashSet<Symbol> symbols, int maxLength = 1000, int maxItems = 10000)
        {
            int len = 0;
            List<List<string>> lists = new();
            List<string> list = new();
            lists.Add(list);

            foreach (Symbol symbol in symbols)
            {
                string str = WebUtility.UrlEncode(symbol.Name); // just encode the symbols
                if (len + str.Length > maxLength || list.Count == maxItems)
                {
                    list = new List<string>();
                    lists.Add(list);
                    len = 0;
                }
                list.Add(str);
                len += str.Length;
            }
            return lists;
        }

        private async Task<List<JsonElement>> MakeRequest(Uri uri, CancellationToken ct)
        {
            Logger.LogInformation(uri.ToString());

            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            if (UseHttpV2)
                request.Version = new Version(2, 0);

            using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

            JsonDocument jsonDocument = await JsonDocument.ParseAsync(stream, default, ct).ConfigureAwait(false);
            if (!jsonDocument.RootElement.TryGetProperty("quoteResponse", out JsonElement quoteResponse))
                throw new InvalidDataException("quoteResponse");
            if (!quoteResponse.TryGetProperty("error", out JsonElement error))
                throw new InvalidDataException("error");
            string? errorMessage = error.GetString();
            if (errorMessage != null)
                throw new InvalidDataException($"Error requesting YahooSnapshot: {errorMessage}.");
            if (!quoteResponse.TryGetProperty("result", out JsonElement result))
                throw new InvalidDataException("result");
            return result.EnumerateArray().ToList();
        }
    }
}

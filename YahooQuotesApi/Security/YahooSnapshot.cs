using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NodaTime;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Net.Http;

// Invalid symbols are ignored by Yahoo.

namespace YahooQuotesApi
{
    internal class YahooSnapshot
    {
        private readonly ILogger Logger;
        private readonly IHttpClientFactory HttpClientFactory;
        private readonly AsyncItemsCache<Symbol, Security?> Cache;

        internal YahooSnapshot(IClock clock, ILogger logger, IHttpClientFactory factory, Duration cacheDuration, Duration snapshotDelay)
        {
            Logger = logger;
            HttpClientFactory = factory;
            Cache = new AsyncItemsCache<Symbol, Security?>(clock, cacheDuration, snapshotDelay, Producer);
        }

        internal async Task<Dictionary<Symbol, Security?>> GetAsync(List<Symbol> symbols, CancellationToken ct = default)
        {
            var currency = symbols.FirstOrDefault(s => s.IsCurrency);
            if (currency != null)
                throw new ArgumentException($"Invalid symbol: {currency} (currency).");

            return await Cache.Get(symbols, ct).ConfigureAwait(false);
        }
        private async Task<Dictionary<Symbol, Security?>> Producer(List<Symbol> symbols, CancellationToken ct)
        {
            var dict = symbols.ToDictionary(s => s, s => (Security?)null);
            var elements = await GetElements(symbols, ct).ConfigureAwait(false);
            foreach (var element in elements)
            {
                var security = new Security(element, Logger);
                var symbol = security.Symbol;
                if (!dict.ContainsKey(symbol))
                    throw new InvalidOperationException(symbol);
                dict[symbol] = security;
            }
            return dict;
        }

        private async Task<IEnumerable<JsonElement>> GetElements(IEnumerable<Symbol> symbols, CancellationToken ct)
        {
            // start tasks
            var tasks = GetUris(symbols).Select(u => MakeRequest(u, ct));
            var responses = await Task.WhenAll(tasks).ConfigureAwait(false);
            return responses.SelectMany(x => x).ToList();
        }

        private static IEnumerable<Uri> GetUris(IEnumerable<Symbol> symbols)
        {
            const string baseUrl = "https://query2.finance.yahoo.com/v7/finance/quote";

            return PartitionSymbols(symbols)
                .Select(s => $"{baseUrl}?symbols={string.Join(",", s)}")
                .Select(s => new Uri(s));
        }

        private static List<List<string>> PartitionSymbols(IEnumerable<Symbol> symbols, int maxLength = 1000, int maxItems = 10000)
        {
            int len = 0;
            var lists = new List<List<string>>();
            var list = new List<string>();
            lists.Add(list);

            foreach (var symbol in symbols)
            {
                var str = WebUtility.UrlEncode(symbol.Name); // just encode the symbols
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
            
            var httpClient = HttpClientFactory.CreateClient("snapshot");
            
            using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            var jsonDocument = await JsonDocument.ParseAsync(stream, default, ct).ConfigureAwait(false);
            if (!jsonDocument.RootElement.TryGetProperty("quoteResponse", out var quoteResponse))
                throw new InvalidDataException("quoteResponse");
            if (!quoteResponse.TryGetProperty("error", out var error))
                throw new InvalidDataException("error");
            var errorMessage = error.GetString();
            if (errorMessage != default)
                throw new InvalidDataException($"Error requesting YahooSnapshot: {errorMessage}.");
            if (!quoteResponse.TryGetProperty("result", out var result))
                throw new InvalidDataException("result");
            return result.EnumerateArray().ToList();
        }
    }
}

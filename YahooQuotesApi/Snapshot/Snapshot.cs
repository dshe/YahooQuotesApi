using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Flurl.Http;
using Flurl.Http.Configuration;

// Invalid symbols are often, but not always, ignored by Yahoo.
// So the number of symbols returned may be less than requested.
// Null is returned for invalid symbols.

namespace YahooQuotesApi
{
    internal class Snapshot
    {
        static Snapshot()
        {
            FlurlHttp.GlobalSettings.JsonSerializer = new NewtonsoftJsonSerializer(
                new JsonSerializerSettings()
                {
                    FloatParseHandling = FloatParseHandling.Decimal
                });
        }

        private readonly ILogger Logger;
        internal Snapshot(ILogger logger) => Logger = logger;

        internal async Task<Dictionary<string, Dictionary<string, object>?>> GetAsync(List<string> symbols, CancellationToken ct)
        {
            var dictionary = new Dictionary<string, Dictionary<string, object>?>(symbols.Count, StringComparer.OrdinalIgnoreCase);
            if (!symbols.Any())
                return dictionary;
            foreach (var symbol in symbols)
                dictionary.Add(symbol, null);

            // start snapshot task(s)
            var tasks = GetUrls(symbols).Select(u => MakeRequest(u, ct)).ToList();

            foreach (var task in tasks)
            {
                dynamic responseExpando = await task.ConfigureAwait(false);
                dynamic quoteExpando = responseExpando.quoteResponse;

                if (quoteExpando.error != null)
                    throw new InvalidDataException($"GetAsync error: {quoteExpando.error}");

                foreach (ExpandoObject resultExpando in quoteExpando.result)
                {
                    IDictionary<string, object> expandoDict = resultExpando;

                    var dict = new Dictionary<string, object>(100, StringComparer.OrdinalIgnoreCase);

                    var ok = expandoDict.TryGetValue("Symbol", out var val);

                    foreach (var kvp in expandoDict)
                        dict.Add(kvp.Key.ToPascal(), kvp.Value);

                    var symbol = (string)dict["Symbol"];

                    if (symbol.EndsWith("=X", StringComparison.OrdinalIgnoreCase) && !dictionary.ContainsKey(symbol)) // sometimes currency USDXXX=X is returned as XXX=X
                    {
                        // Sometimes currency USDXXX=X is returned as XXX=X, and vice-versa.
                        string? newSymbol = null;
                        if (symbol.Length == 5) 
                            newSymbol = "USD" + symbol;
                        else if (symbol.Length == 8 && symbol.StartsWith("USD", StringComparison.OrdinalIgnoreCase))
                            newSymbol = symbol.Substring(3);
                        if (newSymbol != null && dictionary.ContainsKey(newSymbol))
                        {
                            symbol = newSymbol;
                            dict["Symbol"] = newSymbol;
                        }
                    }

                    FieldModifier.Modify(symbol, dict);

                    if (!dictionary.TryGetValue(symbol, out var value))
                        throw new Exception($"Symbol not found, and may have changed: {symbol}.");
                    if (value != null)
                        throw new Exception($"Symbol already set: {symbol}.");
                    dictionary[symbol] = dict;
                }
            }

            return dictionary;
        }

        private static List<string> GetUrls(List<string> symbols)
        {
            const string baseUrl = "https://query2.finance.yahoo.com/v7/finance/quote";
            return GetLists(symbols)
                .Select(s => $"{baseUrl}?symbols={string.Join(",", s)}")
                .ToList();
        }

        private static List<List<string>> GetLists(List<string> strings, int maxLength = 1000, int maxItems = 10000)
        {
            int len = 0;
            var lists = new List<List<string>>();
            var list = new List<string>();
            lists.Add(list);

            foreach (var s in strings)
            {
                var str = WebUtility.UrlEncode(s); // just encode the symbols
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

        private async Task<dynamic> MakeRequest(string url, CancellationToken ct)
        {
            Logger.LogInformation(url);
            return await url.GetJsonAsync(ct).ConfigureAwait(false); // ExpandoObject
        }
    }
}

using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Invalid symbols are often, but not always, ignored by Yahoo.
// So the number of symbols returned may be less than requested.
// When multiple symbols are requested, null is returned for invalid symbols.

namespace YahooQuotesApi
{
    public sealed class YahooSnapshot
    {
        private readonly ILogger Logger;
        private readonly List<string> FieldNames = new List<string>();
        private readonly ConcurrentDictionary<string, Security?> SecuritiesCache = new ConcurrentDictionary<string, Security?>();
        private readonly bool UseCache;

        public YahooSnapshot(bool useCache = false, ILogger<YahooSnapshot>? logger = null)
        {
            Logger = logger ?? NullLogger<YahooSnapshot>.Instance;
            UseCache = useCache;
        }

        public YahooSnapshot Fields(params Field[] fields) => Fields(fields.ToList());
        public YahooSnapshot Fields(IEnumerable<Field> fields) => Fields(fields.Select(f => f.ToString()).ToList());
        public YahooSnapshot Fields(params string[] fields) => Fields(fields.ToList());
        public YahooSnapshot Fields(IEnumerable<string> fields)
        {
            var fieldList = fields.ToList();
            if (!fieldList.Any() || fieldList.Any(x => string.IsNullOrWhiteSpace(x)))
                throw new ArgumentException(nameof(fieldList));
            FieldNames.AddRange(fieldList);
            var duplicate = FieldNames.CaseInsensitiveDuplicates().FirstOrDefault();
            if (duplicate != null)
                throw new ArgumentException($"Duplicate field: {duplicate}.", nameof(fieldList));
            return this;
        }

        public async Task<Security> GetAsync(string symbol, CancellationToken ct = default)
        {
            var securities = await GetAsync(new string[] { symbol }, ct).ConfigureAwait(false);
            var security = securities.Single().Value;
            if (security == null)
                throw new Exception($"Unknown symbol: {symbol}.");
            return security;
        }

        public async Task<Dictionary<string, Security?>> GetAsync(IEnumerable<string> symbols, CancellationToken ct = default)
        {
            var syms = Utility.CheckSymbols(symbols);
            if (!syms.Any())
                return new Dictionary<string, Security?>();
            
            var securities = new Dictionary<string, Security?>(StringComparer.OrdinalIgnoreCase);
            if (UseCache)
            {
                foreach (var symbol in syms)
                {
                    if (!SecuritiesCache.TryGetValue(symbol, out Security? security))
                        break;
                    securities.Add(symbol, security);
                }
                if (securities.Count == syms.Count())
                    return securities;
            }

            foreach (var symbol in syms)
                securities[symbol] = null;

            foreach (var task in GetUrls(syms, FieldNames).Select(u => MakeRequest(u, ct)))
            {
                dynamic expando;
                try
                {
                    expando = await task.ConfigureAwait(false);
                }
                catch (FlurlHttpException ex) when (ex.Call.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    // If there are no valid symbols, this exception is thrown by Flurl
                    continue;
                }

                dynamic quoteExpando = expando.quoteResponse;

                if (quoteExpando.error != null)
                    throw new InvalidDataException($"GetAsync error: {quoteExpando.error}");

                foreach (IDictionary<string, dynamic> dictionary in quoteExpando.result)
                    securities[dictionary["symbol"]] = new Security(dictionary);
            }
            if (UseCache)
            {
                foreach (var security in securities)
                    SecuritiesCache[security.Key] = security.Value; // add or update
            }
            return securities;
        }

        private async Task<dynamic> MakeRequest(string url, CancellationToken ct)
        {
            Logger.LogInformation(url);
            // ExpandoObject
            return await url.GetJsonAsync(ct).ConfigureAwait(false);
        }

        private static List<string> GetUrls(IEnumerable<string> symbols, List<string> fields)
        {
            const string baseUrl = "https://query2.finance.yahoo.com/v7/finance/quote";
            string fieldsUrl = fields.Any() ? $"&fields={string.Join(",", fields)}" : "";

            return GetLists(symbols)
                .Select(s => "?symbols=" + string.Join(",", s))
                .Select(s => baseUrl + s + fieldsUrl)
                .ToList();
        }

        private static List<List<string>> GetLists(IEnumerable<string> strings, int maxLength = 1000, int maxItems = 10000)
        {
            int len = 0;
            var lists = new List<List<string>>();
            var list = new List<string>();
            lists.Add(list);

            var enumerator = strings.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var str = enumerator.Current;
                str = WebUtility.UrlEncode(str); // just encode the symbols (some con
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

    }
}

using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    // Invalid symbols are often, but not always, ignored by Yahoo.
    // So the number of symbols returned may be less than requested.

    public sealed class YahooSnapshot
    {
        private readonly ILogger Logger;
        private readonly List<string> FieldNames = new List<string>();

        public YahooSnapshot() : this(NullLogger<YahooSnapshot>.Instance) { }
        public YahooSnapshot(ILogger<YahooSnapshot> logger) => Logger = logger;

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

        public async Task<Security?> GetAsync(string symbol, CancellationToken ct = default)
        {
            var securities = await GetAsync(new string[] { symbol }, ct).ConfigureAwait(false);
            return securities.Single().Value;
        }

        public async Task<Dictionary<string, Security?>> GetAsync(IEnumerable<string> symbols, CancellationToken ct = default)
        {
            if (symbols == null)
                throw new ArgumentNullException(nameof(symbols));
            if (!symbols.Any())
                return new Dictionary<string, Security?>();
            if (symbols.Any(s => string.IsNullOrEmpty(s) || s.Contains(" ")))
                throw new ArgumentException(nameof(symbols));
            symbols = new HashSet<string>(symbols, StringComparer.OrdinalIgnoreCase); // ignore duplicates
            var securities = new Dictionary<string, Security?>(StringComparer.OrdinalIgnoreCase);
            foreach (var symbol in symbols)
                securities.Add(symbol, null);

            var urls = GetUrls(symbols, FieldNames);
            var tasks = urls.Select(u => MakeRequest(u, ct));

            foreach (var task in tasks)
            {
                dynamic expando;
                try
                {
                    expando = await task.ConfigureAwait(false);
                }
                catch (FlurlHttpException ex) when (ex.Call.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
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
            return securities;
        }

        private async Task<dynamic> MakeRequest(string url, CancellationToken ct)
        {
            Logger.LogInformation(url);

            return await url
                .GetAsync(ct)
                .ReceiveJson() // ExpandoObject
                .ConfigureAwait(false);
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

        private static List<List<string>> GetLists(IEnumerable<string> strings, int maxLength = 1000)
        {
            int len = 0;
            var lists = new List<List<string>>();
            var list = new List<string>();
            lists.Add(list);

            var enumerator = strings.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var str = enumerator.Current;
                if (len + str.Length > maxLength)
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

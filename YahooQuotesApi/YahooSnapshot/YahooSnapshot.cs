using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    // Invalid symbols are often, but not always, ignored by Yahoo.
    // So the number of symbols returned may be less than requested.

    public sealed class YahooSnapshot
    {
        private readonly ILogger Logger;
        private readonly CancellationToken Ct;
        private readonly List<string> FieldNames = new List<string>();

        public YahooSnapshot(CancellationToken ct = default) : this(NullLogger<YahooSnapshot>.Instance, ct) { }
        public YahooSnapshot(ILogger<YahooSnapshot> logger, CancellationToken ct = default) => (Logger, Ct) = (logger, ct);

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

        public async Task<Security?> GetAsync(string symbol)
        {
            dynamic expando;

            try
            {
                expando = await MakeRequest(new[] { symbol }, FieldNames).ConfigureAwait(false);
            }
            catch (FlurlHttpException ex) when(ex.Call.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null; // invalid symbol
            }

            dynamic quoteExpando = expando.quoteResponse;

            if (quoteExpando.error != null)
                throw new InvalidDataException($"GetAsync error: {quoteExpando.error}");

            dynamic result = quoteExpando.result;

            if (result.Count == 0) // invalid symbol
                return null;

            IDictionary<string, dynamic> dictionary = quoteExpando.result[0];

            var security = new Security(dictionary);

            return security;
        }

        public async Task<Dictionary<string, Security?>> GetAsync(IEnumerable<string> symbols)
        {
            var symbolList = symbols.ToList();
            var securities = new Dictionary<string, Security?>(StringComparer.OrdinalIgnoreCase);
            foreach (var symbol in symbolList)
                securities.Add(symbol, null);

            dynamic expando;

            try
            {
                expando = await MakeRequest(symbolList, FieldNames).ConfigureAwait(false);
            }
            catch (FlurlHttpException ex) when (ex.Call.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // If there are no valid symbols, this exception is thrown by Flurl
                return securities;
            }

            dynamic quoteExpando = expando.quoteResponse;

            if (quoteExpando.error != null)
                throw new InvalidDataException($"GetAsync error: {quoteExpando.error}");

            foreach (IDictionary<string, dynamic> dictionary in quoteExpando.result)
                securities[dictionary["symbol"]] = new Security(dictionary);

            return securities;
        }

        private async Task<dynamic> MakeRequest(IList<string> symbols, List<string> fields)
        {
            if (symbols.Any(x => string.IsNullOrWhiteSpace(x)) || !symbols.Any())
                throw new ArgumentException(nameof(symbols));
            var duplicateSymbol = symbols.CaseInsensitiveDuplicates().FirstOrDefault();
            if (duplicateSymbol != null)
                throw new ArgumentException($"Duplicate symbol: {duplicateSymbol}.");

            // IsEncoded = true: do not encode commas
            var url = "https://query2.finance.yahoo.com/v7/finance/quote"
                .SetQueryParam("symbols", string.Join(",", symbols), true);

            if (fields.Any())
                url = url.SetQueryParam("fields", string.Join(",", fields), true);

            Logger.LogInformation(url);

            return await url
                .GetAsync(Ct)
                .ReceiveJson() // ExpandoObject
                .ConfigureAwait(false);
        }
    }
}

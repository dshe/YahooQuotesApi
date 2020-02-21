using CsvHelper;
using Flurl;
using Flurl.Http;
using NodaTime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;

namespace YahooQuotesApi
{
    public sealed class YahooHistory
    {
        private readonly ILogger Logger;
        private readonly CancellationToken Ct;
        private Instant Start = Instant.MinValue, End = Instant.MaxValue;
        private Frequency Frequency = Frequency.Daily;

        public YahooHistory(ILogger<YahooHistory> logger, CancellationToken ct = default) => (Logger, Ct) = (logger, ct);
        public YahooHistory(CancellationToken ct = default) : this(NullLogger<YahooHistory>.Instance, ct) { }

        public YahooHistory Period(Instant start, Instant end)
        {
            if (start > end)
                throw new ArgumentException("start > end");
            Start = start;
            End = end;
            return this;
        }
        public YahooHistory Period(Instant start) => Period(start, Instant.MaxValue);
        public YahooHistory Period(int days) => Period(Utility.Clock.GetCurrentInstant().Minus(Duration.FromDays(days))); // approximate

        public Task<List<PriceTick>?> GetPricesAsync(string symbol, Frequency frequency = Frequency.Daily) =>
            GetTicksAsync<PriceTick>(symbol, frequency);

        public Task<Dictionary<string, List<PriceTick>?>> GetPricesAsync(IEnumerable<string> symbols, Frequency frequency = Frequency.Daily) =>
            GetTicksAsync<PriceTick>(symbols, frequency);

        public Task<List<DividendTick>?> GetDividendsAsync(string symbol) =>
            GetTicksAsync<DividendTick>(symbol);

        public Task<Dictionary<string, List<DividendTick>?>> GetDividendsAsync(IEnumerable<string> symbols) =>
            GetTicksAsync<DividendTick>(symbols);

        public Task<List<SplitTick>?> GetSplitsAsync(string symbol) =>
            GetTicksAsync<SplitTick>(symbol);

        public Task<Dictionary<string, List<SplitTick>?>> GetSplitsAsync(IEnumerable<string> symbols) =>
            GetTicksAsync<SplitTick>(symbols);

        private async Task<Dictionary<string, List<ITick>?>> GetTicksAsync<ITick>(IEnumerable<string> symbols, Frequency frequency = Frequency.Daily) where ITick : class
        {
            if (symbols == null)
                throw new ArgumentNullException(nameof(symbols));
            
            var symbolList = symbols.ToList();

            if (!symbolList.Any())
                throw new ArgumentException("Empty list.", nameof(symbolList));

            var duplicates = symbolList.CaseInsensitiveDuplicates();
            if (duplicates.Any())
            {
                var msg = "Duplicate symbol(s): " + duplicates.Select(s => "\"" + s + "\"").ToCommaDelimitedList() + ".";
                throw new ArgumentException(msg, nameof(symbolList));
            }

            // create a list of started tasks
            var tasks = symbolList.Select(symbol => GetTicksAsync<ITick>(symbol, frequency)).ToList();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var dictionary = tasks.Select((task, i) => (task, i))
                .ToDictionary(x => symbolList[x.i], x => x.task.Result, StringComparer.OrdinalIgnoreCase);

            return dictionary;
        }

        private async Task<List<ITick>?> GetTicksAsync<ITick>(string symbol, Frequency frequency = Frequency.Daily) where ITick : class
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Empty string.", nameof(symbol));

            Frequency = frequency;
            string tickParam = TickParser.GetParamFromType<ITick>();

            try
            {
                return await GetTickResponseAsync<ITick>(symbol, tickParam).ConfigureAwait(false);
            }
            catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.LogInformation($"Symbol not found: \"{symbol}\".");
                return null;
            }
        }

        private async Task<List<ITick>> GetTickResponseAsync<ITick>(string symbol, string tickParam) where ITick:class
        {
            using var stream = await GetResponseStreamAsync(symbol, tickParam).ConfigureAwait(false);
            using var sr = new StreamReader(stream);
            //var str = await sr.ReadToEndAsync().ConfigureAwait(false);
            using var csvReader = new CsvReader(sr, CultureInfo.InvariantCulture);

            var ticks = new List<ITick>();

            if (!csvReader.Read()) // skip header
                throw new Exception("Did not read headers.");

            while (csvReader.Read())
            {
                var tick = TickParser.Parse<ITick>(csvReader.Context.Record);
                if (tick != null)
                    ticks.Add(tick);
            }
            return ticks;
        }

        private async Task<Stream> GetResponseStreamAsync(string symbol, string tickParam)
        {
            bool reset = false;
            while (true)
            {
                try
                {
                    var (client, crumb) = await ClientFactory.GetClientAndCrumbAsync(reset, Logger, Ct).ConfigureAwait(false);
                    return await _GetResponseStreamAsync(client, crumb).ConfigureAwait(false);
                }
                catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == HttpStatusCode.Unauthorized && !reset)
                {
                    Logger.LogDebug("GetResponseStreamAsync: Unauthorized. Retrying.");
                    reset = true;
                }
                //catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == HttpStatusCode.NotFound)
                //  throw new Exception($"Invalid symbol '{symbol}'.", ex);
            }

            #region Local Function

            Task<Stream> _GetResponseStreamAsync(IFlurlClient _client, string _crumb)
            {
                var url = "https://query1.finance.yahoo.com/v7/finance/download"
                    .AppendPathSegment(symbol)
                    .SetQueryParam("period1", Start == Instant.MinValue ? 0 : Start.ToUnixTimeSeconds())
                    .SetQueryParam("period2", End.ToUnixTimeSeconds())
                    .SetQueryParam("interval", $"1{Frequency.Name()}")
                    .SetQueryParam("events", tickParam)
                    .SetQueryParam("crumb", _crumb);

                Logger.LogInformation(url);

                return url
                    .WithClient(_client)
                    .GetAsync(Ct)
                    .ReceiveStream();
            }

            #endregion
        }
    }
}

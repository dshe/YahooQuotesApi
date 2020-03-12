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
        private Instant Start = Instant.MinValue, End = Instant.MaxValue;

        public YahooHistory(ILogger<YahooHistory> logger) => (Logger) = (logger);
        public YahooHistory() : this(NullLogger<YahooHistory>.Instance) { }

        public YahooHistory Period(Instant start, Instant end)
        {
            if (start > end)
                throw new ArgumentException("start > end");
            Start = start;
            End = end;
            return this;
        }
        public YahooHistory Period(Instant start) => Period(start, Instant.MaxValue);
        public YahooHistory Period(int days) => Period(Utility.Clock.GetCurrentInstant().Minus(Duration.FromDays(days)));

        public Task<List<PriceTick>?> GetPricesAsync(string symbol, Frequency frequency = Frequency.Daily, CancellationToken ct = default) =>
            GetTicksAsync<PriceTick>(symbol, frequency, ct);

        public Task<Dictionary<string, List<PriceTick>?>> GetPricesAsync(IEnumerable<string> symbols, Frequency frequency = Frequency.Daily, CancellationToken ct = default) =>
            GetTicksAsync<PriceTick>(symbols, frequency, ct);

        public Task<List<DividendTick>?> GetDividendsAsync(string symbol, CancellationToken ct = default) =>
            GetTicksAsync<DividendTick>(symbol, Frequency.Daily, ct);

        public Task<Dictionary<string, List<DividendTick>?>> GetDividendsAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            GetTicksAsync<DividendTick>(symbols, Frequency.Daily, ct);

        public Task<List<SplitTick>?> GetSplitsAsync(string symbol, CancellationToken ct = default) =>
            GetTicksAsync<SplitTick>(symbol, Frequency.Daily, ct);

        public Task<Dictionary<string, List<SplitTick>?>> GetSplitsAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            GetTicksAsync<SplitTick>(symbols, Frequency.Daily, ct);

        private async Task<Dictionary<string, List<ITick>?>> GetTicksAsync<ITick>(
            IEnumerable<string> symbols, Frequency frequency = Frequency.Daily, CancellationToken ct = default) where ITick : class
        {
            if (symbols == null)
                throw new ArgumentNullException(nameof(symbols));
            if (!symbols.Any())
                return new Dictionary<string, List<ITick>?>();
            symbols = new HashSet<string>(symbols, StringComparer.OrdinalIgnoreCase); // ignore duplicates
            var tasks = symbols.Select(symbol => GetTicksAsync<ITick>(symbol, frequency, ct));
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return symbols.Zip(tasks, (symbol, task) => (symbol, task.Result))
                .ToDictionary(x => x.symbol, x => x.Result, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<List<ITick>?> GetTicksAsync<ITick>(string symbol, Frequency frequency = Frequency.Daily, CancellationToken ct = default) where ITick : class
        {
            if (string.IsNullOrEmpty(symbol) || symbol.Contains(" "))
                throw new ArgumentNullException(nameof(symbol));
            try
            {
                return await GetTickResponseAsync<ITick>(symbol, frequency, ct).ConfigureAwait(false);
            }
            catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.LogInformation($"Symbol not found: \"{symbol}\".");
                return null;
            }
        }

        private async Task<List<ITick>> GetTickResponseAsync<ITick>(string symbol, Frequency frequency, CancellationToken ct) where ITick:class
        {
            string tickParam = TickParser.GetParamFromType<ITick>();
            using var stream = await GetResponseStreamAsync(symbol, tickParam, frequency, ct).ConfigureAwait(false);
            using var sr = new StreamReader(stream);
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

        private async Task<Stream> GetResponseStreamAsync(string symbol, string tickParam, Frequency frequency, CancellationToken ct)
        {
            bool reset = false;
            while (true)
            {
                try
                {
                    var (client, crumb) = await ClientFactory.GetClientAndCrumbAsync(reset, Logger, ct).ConfigureAwait(false);
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
                    .SetQueryParam("interval", $"1{frequency.Name()}")
                    .SetQueryParam("events", tickParam)
                    .SetQueryParam("crumb", _crumb);

                Logger.LogInformation(url);

                return url
                    .WithClient(_client)
                    .GetAsync(ct)
                    .ReceiveStream();
            }

            #endregion
        }
    }
}

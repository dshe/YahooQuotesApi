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
        private Frequency Freq = YahooQuotesApi.Frequency.Daily;
        private Instant Start = Instant.MinValue;
        private readonly YahooSnapshot YahooSnapshot;
        private readonly AsyncLazyCache<string, Security?> SnapshotCache;
        private readonly AsyncLazyCache<string, List<object>?> HistoryCache;
        public async Task<Security?> GetSnapshot(string symbol, CancellationToken ct = default) =>
            await SnapshotCache.Get(symbol, ct).ConfigureAwait(false);

        public YahooHistory(ILogger<YahooHistory>? logger = null)
        {
            Logger = logger ?? NullLogger<YahooHistory>.Instance;
            YahooSnapshot = new YahooSnapshot(); // take Logger
            HistoryCache = new AsyncLazyCache<string, List<object>?>(Producer);
            SnapshotCache = new AsyncLazyCache<string, Security?>
                (async(s,c) => await YahooSnapshot.GetAsync(s, c).ConfigureAwait(false));
        }

        public YahooHistory FromDate(Instant start)
        {
            Start = start;
            HistoryCache.Clear();
            return this;
        }
        public YahooHistory FromDate(int days) => FromDate(Utility.Clock.GetCurrentInstant().Minus(Duration.FromDays(days)));

        public YahooHistory Frequency(Frequency frequency)
        {
            Freq = frequency;
            return this;
        }

        public Task<List<PriceTick>?> GetPricesAsync(string symbol, CancellationToken ct = default) =>
            GetTicksAsync<PriceTick>(symbol, ct);

        public Task<Dictionary<string, List<PriceTick>?>> GetPricesAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            GetTicksAsync<PriceTick>(symbols, ct);

        public Task<List<DividendTick>?> GetDividendsAsync(string symbol, CancellationToken ct = default) =>
            GetTicksAsync<DividendTick>(symbol, ct);

        public Task<Dictionary<string, List<DividendTick>?>> GetDividendsAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            GetTicksAsync<DividendTick>(symbols, ct);

        public Task<List<SplitTick>?> GetSplitsAsync(string symbol, CancellationToken ct = default) =>
            GetTicksAsync<SplitTick>(symbol, ct);

        public Task<Dictionary<string, List<SplitTick>?>> GetSplitsAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            GetTicksAsync<SplitTick>(symbols, ct);
        
        private async Task<Dictionary<string, List<T>?>> GetTicksAsync<T>(
            IEnumerable<string> symbols, CancellationToken ct)
        {
            if (symbols == null)
                throw new ArgumentNullException(nameof(symbols));
            if (!symbols.Any())
                return new Dictionary<string, List<T>?>();
            var tasks = symbols.Distinct(StringComparer.CurrentCultureIgnoreCase).Select(symbol => GetTicksAsync<T>(symbol, ct));
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return symbols.Zip(tasks, (symbol, task) => (symbol, task.Result))
                .ToDictionary(x => x.symbol, x => x.Result, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<List<T>?> GetTicksAsync<T>(string symbol, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol));

            var key = $"{symbol},{TickParser.GetParamFromType<T>()},{Freq.Name()}";
            var result = await HistoryCache.Get(key, ct).ConfigureAwait(false);
            if (result == null) // tricky
                return null;
            return result.Cast<T>().ToList(); // tricky
        }

        private async Task<List<object>?> Producer(string key, CancellationToken ct)
        {
            var parts = key.Split(',');
            var symbol = parts[0];
            var param = parts[1];
            var frequency = parts[2];
            try
            {
                return await GetTickResponseAsync(symbol, param, frequency, ct).ConfigureAwait(false); ;
            }
            catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.LogInformation($"Symbol not found: \"{symbol}\".");
                return null;
            }
        }

        private async Task<List<object>?> GetTickResponseAsync(string symbol, string param, string frequency, CancellationToken ct) 
        {
            var task = GetTimeZone(symbol, ct); // start task
            using var stream = await GetResponseStreamAsync(symbol, param, frequency, ct).ConfigureAwait(false);
            using var sr = new StreamReader(stream);
            using var csvReader = new CsvReader(sr, CultureInfo.InvariantCulture);

            var closeTime = Exchanges.GetCloseTimeFromSymbol(symbol);
            var tz = await task.ConfigureAwait(false);
            if (tz == null)
                return null; // invalid symbol

            if (!csvReader.Read()) // skip header
                throw new Exception("Did not read headers.");
            var ticks = new List<object>();
            while (csvReader.Read())
            {
                var tick = TickParser.Parse(param, csvReader.Context.Record, closeTime, tz);
                if (tick != null)
                    ticks.Add(tick);
            }
            return ticks;
        }

        private async Task<DateTimeZone?> GetTimeZone(string symbol, CancellationToken ct)
        {
            var security = await GetSnapshot(symbol, ct).ConfigureAwait(false);
            if (security == null)
                return null;
            var tzName = security.ExchangeTimezoneName;
            if (tzName == null)
                throw new InvalidDataException($"No timezone available for symbol: {symbol}.");
            var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(tzName) ?? throw new TimeZoneNotFoundException(tzName);
            return tz;
        }

        private async Task<Stream> GetResponseStreamAsync(string symbol, string param, string frequency, CancellationToken ct)
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
                    .SetQueryParam("period2", Instant.MaxValue.ToUnixTimeSeconds())
                    .SetQueryParam("interval", $"1{frequency}")
                    .SetQueryParam("events", param)
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

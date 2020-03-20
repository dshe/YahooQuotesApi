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
        private readonly YahooSnapshot YahooSnapshot = new YahooSnapshot(useCache:true);
        private readonly AsyncLazyCache<string, List<object>> HistoryCache;
        public static LocalTime GetCloseTimeFromSymbol(string symbol) => Exchanges.GetCloseTimeFromSymbol(symbol);

        public YahooHistory(ILogger<YahooHistory>? logger = null)
        {
            Logger = logger ?? NullLogger<YahooHistory>.Instance;
            HistoryCache = new AsyncLazyCache<string, List<object>>(Producer);
        }

        public YahooHistory FromDate(Instant start)
        {
            Start = start;
            HistoryCache.Clear();
            return this;
        }
        public YahooHistory FromDays(int days) => FromDate(Utility.Clock.GetCurrentInstant().Minus(Duration.FromDays(days)));

        public YahooHistory Frequency(Frequency frequency)
        {
            Freq = frequency;
            return this;
        }

        public Task<List<PriceTick>> GetPricesAsync(string symbol, CancellationToken ct = default) =>
            GetTicksAsync<PriceTick>(symbol, ct);
        public Task<List<DividendTick>> GetDividendsAsync(string symbol, CancellationToken ct = default) =>
            GetTicksAsync<DividendTick>(symbol, ct);
        public Task<List<SplitTick>> GetSplitsAsync(string symbol, CancellationToken ct = default) =>
            GetTicksAsync<SplitTick>(symbol, ct);


        public Task<Dictionary<string, List<PriceTick>?>> GetPricesAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            GetTicksAsync<PriceTick>(symbols, ct);
        public Task<Dictionary<string, List<DividendTick>?>> GetDividendsAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            GetTicksAsync<DividendTick>(symbols, ct);
        public Task<Dictionary<string, List<SplitTick>?>> GetSplitsAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            GetTicksAsync<SplitTick>(symbols, ct);

        private async Task<Dictionary<string, List<T>?>> GetTicksAsync<T>(IEnumerable<string> symbols, CancellationToken ct)
        {
            var syms = Utility.CheckSymbols(symbols);
            if (!syms.Any())
                return new Dictionary<string, List<T>?>();

            // start tasks
            var snapshotTask = YahooSnapshot.GetAsync(symbols); // cache
            var items = syms.Select(s => (s, GetTicksAsync<T>(s, ct)));
            await snapshotTask.ConfigureAwait(false);

            var dictionary = new Dictionary<string, List<T>?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (symbol, task) in items)
            {
                List<T>? result;
                try
                {
                    result = await task.ConfigureAwait(false);
                }
                catch (Exception ex) when (ex.Message.StartsWith("Unknown symbol"))
                {
                    result = null;
                }
                dictionary.Add(symbol, result);
            }
            return dictionary;
        }


        private async Task<List<T>> GetTicksAsync<T>(string symbol, CancellationToken ct)
        {
            var sym = Utility.CheckSymbol(symbol);
            var key = $"{sym},{TickParser.GetParamFromType<T>()},{Freq.Name()}";
            var result = await HistoryCache.Get(key, ct).ConfigureAwait(false);
            return result.Cast<T>().ToList(); // tricky
        }

        private async Task<List<object>> Producer(string key, CancellationToken ct)
        {
            var parts = key.Split(',');
            var symbol = parts[0];
            var param = parts[1];
            var frequency = parts[2];
            return await GetTickResponseAsync(symbol, param, frequency, ct).ConfigureAwait(false); ;
        }

        private async Task<List<object>> GetTickResponseAsync(string symbol, string param, string frequency, CancellationToken ct) 
        {
            var closeTime = GetCloseTimeFromSymbol(symbol);
            var tz = await GetTimeZone(symbol, ct).ConfigureAwait(false);
            using var stream = await GetResponseStreamAsync(symbol, param, frequency, ct).ConfigureAwait(false);
            using var sr = new StreamReader(stream);
            using var csvReader = new CsvReader(sr, CultureInfo.InvariantCulture);

            if (!csvReader.Read()) // skip header
                throw new Exception("Could not read headers.");
            var ticks = new List<object>();
            while (csvReader.Read())
            {
                var tick = TickParser.Parse(param, csvReader.Context.Record, closeTime, tz);
                if (tick != null)
                    ticks.Add(tick);
            }
            return ticks;
        }

        private async Task<DateTimeZone> GetTimeZone(string symbol, CancellationToken ct)
        {
            var security = await YahooSnapshot.GetAsync(symbol, ct).ConfigureAwait(false); // cached
            var tzName = security.ExchangeTimezoneName ?? throw new TimeZoneNotFoundException($"No Timezone found for symbol: {symbol}.");
            return DateTimeZoneProviders.Tzdb.GetZoneOrNull(tzName) ?? throw new InvalidTimeZoneException(tzName);
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
                catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception($"Unknown symbol '{symbol}'.", ex);
                }
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

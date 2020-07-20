using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using Microsoft.Extensions.Logging;
using NodaTime;
using Flurl;
using Flurl.Http;

namespace YahooQuotesApi
{
    internal sealed class History
    {
        private readonly ILogger Logger;
        private readonly Instant Start;
        private readonly AsyncLazyCache<string, List<object>> Cache;

        internal History(ILogger logger, Instant start, Duration cacheDuration)
        {
            Logger = logger;
            Start = start;
            Cache = new AsyncLazyCache<string, List<object>>(cacheDuration);
        }

        internal async Task<List<PriceTick>?> GetPricesAsync(string symbol, Frequency frequency, LocalTime closeTime, DateTimeZone tz, CancellationToken ct) =>
            await GetTicksAsync<PriceTick>(symbol, frequency, closeTime, tz, ct).ConfigureAwait(false);

        internal async Task<List<DividendTick>?> GetDividendsAsync(string symbol, CancellationToken ct) =>
            await GetTicksAsync<DividendTick>(symbol, Frequency.Daily, null, null, ct).ConfigureAwait(false);

        internal async Task<List<SplitTick>?> GetSplitsAsync(string symbol, CancellationToken ct) =>
            await GetTicksAsync<SplitTick>(symbol, Frequency.Daily, null, null, ct).ConfigureAwait(false);

        private async Task<List<T>?> GetTicksAsync<T>(string symbol, Frequency frequency, LocalTime? closeTime, DateTimeZone? tz, CancellationToken ct)
        {
            var type = typeof(T);
            var key = $"{symbol},{type.Name},{frequency.Name()}";
            try
            {
                var ticks = await Cache.Get(key, () => Produce(symbol, type, frequency, closeTime, tz, ct)).ConfigureAwait(false);
                return ticks.Cast<T>().ToList();
            }
            catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private async Task<List<object>> Produce(string symbol, Type type, Frequency frequency, LocalTime? closeTime, DateTimeZone? tz, CancellationToken ct)
        {
            using var stream = await GetResponseStreamAsync(symbol, type, frequency, ct).ConfigureAwait(false);
            using var streamReader = new StreamReader(stream);
            return TickParser.GetTicks(streamReader, type, closeTime, tz);
        }

        private async Task<Stream> GetResponseStreamAsync(string symbol, Type type, Frequency frequency, CancellationToken ct)
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
                    Logger.LogDebug($"No history for symbol: {symbol}.");
                    throw;
                }
            }

            Task<Stream> _GetResponseStreamAsync(IFlurlClient _client, string _crumb)
            {
                var url = "https://query1.finance.yahoo.com/v7/finance/download"
                    .AppendPathSegment(symbol)
                    .SetQueryParam("period1", Start == Instant.MinValue ? 0 : Start.ToUnixTimeSeconds())
                    .SetQueryParam("period2", Instant.MaxValue.ToUnixTimeSeconds())
                    .SetQueryParam("interval", $"1{frequency.Name()}")
                    .SetQueryParam("events", GetParam(type))
                    .SetQueryParam("crumb", _crumb)
                    .ToString();

                Logger.LogInformation(url);

                return url
                    .WithClient(_client)
                    .GetAsync(ct)
                    .ReceiveStream();
            }

            static string GetParam(Type type)
            {
                if (type == typeof(PriceTick))
                    return "history";
                else if (type == typeof(DividendTick))
                    return "div";
                else if (type == typeof(SplitTick))
                    return "split";
                throw new Exception("GetParam: invalid type.");
            }
        }
    }
}

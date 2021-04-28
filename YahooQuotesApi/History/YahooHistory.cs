using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    internal sealed class YahooHistory
    {
        private readonly ILogger Logger;
        private readonly Instant Start;
        private readonly Frequency PriceHistoryFrequency;
        private readonly ParallelProducerCache<string, Result<ITick[]>> Cache;
        private readonly YahooHistoryRequester YahooHistoryRequester;

        internal YahooHistory(IClock clock, ILogger logger, IHttpClientFactory httpClientFactory, Instant start, Duration cacheDuration, Frequency frequency, bool useHttpV2)
        {
            Logger = logger;
            Start = start;
            PriceHistoryFrequency = frequency;
            YahooHistoryRequester = new YahooHistoryRequester(logger, httpClientFactory, useHttpV2);
            Cache = new ParallelProducerCache<string, Result<ITick[]>>(clock, cacheDuration);
        }

        internal async Task<Result<T[]>> GetTicksAsync<T>(Symbol symbol, CancellationToken ct) where T:ITick
        {
            if (symbol.IsCurrency || symbol.IsEmpty)
                throw new ArgumentException($"Invalid symbol: '{nameof(symbol)}'.");
            var type = typeof(T);
            var frequency = type == typeof(PriceTick) ? PriceHistoryFrequency : Frequency.Daily;
            var key = $"{symbol},{type.Name},{frequency.Name()}";
            try
            {
                var result = await Cache.Get(key, () => Produce<T>(symbol.Name, frequency, ct)).ConfigureAwait(false);
                if (result.HasError)
                    return Result<T[]>.Fail(result.Error);
                return result.Value.Cast<T>().ToArray().ToResult(); // returns a mutable shallow copy
            }
            catch (Exception e)
            {
                Logger.LogCritical(e, $"History error: {e.Message}.");
                throw;
            }
        }

        private async Task<Result<ITick[]>> Produce<T>(string symbol, Frequency frequency, CancellationToken ct) where T: ITick
        {
            var parm = typeof(T).Name switch
            {
                nameof(PriceTick) => "history",
                nameof(DividendTick) => "div",
                nameof(SplitTick) => "split",
                _ => throw new Exception("type")
            };

            var uri = new StringBuilder()
                .Append("https://query2.finance.yahoo.com/v7/finance/download/")
                .Append(symbol)
                .Append($"?period1={(Start == Instant.MinValue ? 0 : Start.ToUnixTimeSeconds())}")
                .Append($"&period2={Instant.MaxValue.ToUnixTimeSeconds()}")
                .Append($"&interval=1{frequency.Name()}")
                .Append($"&events={parm}")
                .ToString()
                .ToUri();

            Logger.LogInformation(uri.ToString());

            using var response = await YahooHistoryRequester.Request(uri, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return Result<ITick[]>.Fail($"History not found.");
            response.EnsureSuccessStatusCode();

            try
            {
                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var streamReader = new StreamReader(stream);
                var ticks = await streamReader.ToTicks<T>().ConfigureAwait(false);
                return Result<ITick[]>.Ok(ticks);
            }
            catch (Exception e)
            {
                return Result<ITick[]>.Fail($"{e.GetType().Name}: {e.Message}");
            }
        }
    }
}

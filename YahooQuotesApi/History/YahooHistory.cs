using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using Microsoft.Extensions.Logging;
using NodaTime;
using System.Net.Http;
using System.Text;

namespace YahooQuotesApi
{
    internal sealed class YahooHistory
    {
        private readonly ILogger Logger;
        private readonly IHttpClientFactory HttpClientFactory;
        private readonly Instant Start;
        private readonly Frequency PriceHistoryFrequency;
        private readonly AsyncItemCache<string, Result<object[]>> Cache;
        private readonly CrumbFactory ClientFactory;

        internal YahooHistory(ILogger logger, IHttpClientFactory factory, Instant start, Duration cacheDuration, Frequency frequency)
        {
            Logger = logger;
            HttpClientFactory = factory;
            ClientFactory = new CrumbFactory(logger, factory);
            Start = start;
            PriceHistoryFrequency = frequency;
            Cache = new AsyncItemCache<string, Result<object[]>>(cacheDuration);
        }

        internal async Task<Result<CandleTick[]>> GetCandlesAsync(Symbol symbol, CancellationToken ct = default) =>
            await GetTicksAsync<CandleTick>(symbol, PriceHistoryFrequency, ct).ConfigureAwait(false);

        internal async Task<Result<DividendTick[]>> GetDividendsAsync(Symbol symbol, CancellationToken ct = default) =>
            await GetTicksAsync<DividendTick>(symbol, Frequency.Daily, ct).ConfigureAwait(false);

        internal async Task<Result<SplitTick[]>> GetSplitsAsync(Symbol symbol, CancellationToken ct = default) =>
            await GetTicksAsync<SplitTick>(symbol, Frequency.Daily, ct).ConfigureAwait(false);

        private async Task<Result<T[]>> GetTicksAsync<T>(Symbol symbol, Frequency frequency, CancellationToken ct)
        {
            if (symbol.IsCurrency || symbol.IsEmpty)
                throw new ArgumentException($"Invalid symbol: '{nameof(symbol)}'.");
            var type = typeof(T);
            var key = $"{symbol},{type.Name},{frequency.Name()}";
            try
            {
                var result = await Cache.Get(key, () => Produce(symbol, type, frequency, ct)).ConfigureAwait(false);
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

        private async Task<Result<object[]>> Produce(string symbol, Type type, Frequency frequency, CancellationToken ct)
        {
            var response = await GetResponseAsync(symbol, type, frequency, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return Result<object[]>.Fail($"History not found.");
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return Result<object[]>.From(() => stream.ToTicks(type));
        }

        private async Task<HttpResponseMessage> GetResponseAsync(string symbol, Type type, Frequency frequency, CancellationToken ct)
        {
            var parm = type.Name switch
            {
                nameof(CandleTick) => "history",
                nameof(DividendTick) => "div",
                nameof(SplitTick) => "split",
                _ => throw new Exception(type.Name)
            };

            bool reset = false;
            while (true)
            {
                var crumb = await ClientFactory.GetCrumbAsync(reset, ct).ConfigureAwait(false);
                var url = new StringBuilder()
                    .Append("https://query2.finance.yahoo.com/v7/finance/download/")
                    .Append(symbol)
                    .Append($"?period1={(Start == Instant.MinValue ? 0 : Start.ToUnixTimeSeconds())}")
                    .Append($"&period2={Instant.MaxValue.ToUnixTimeSeconds()}")
                    .Append($"&interval=1{frequency.Name()}")
                    .Append($"&events={parm}")
                    .Append($"&crumb={crumb}")
                    .ToString();

                Logger.LogInformation(url);

                var response = await HttpClientFactory.CreateClient("history").GetAsync(url).ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.Unauthorized || reset)
                    return response;

                Logger.LogError("GetResponse: Unauthorized. Retrying...");
                reset = true;
            }
        }
    }
}

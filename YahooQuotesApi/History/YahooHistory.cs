using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace YahooQuotesApi;

internal sealed class YahooHistory
{
    private readonly ILogger Logger;
    private readonly Instant Start;
    private readonly Frequency PriceHistoryFrequency;
    private readonly ParallelProducerCache<string, Result<ITick[]>> Cache;
    private readonly YahooHistoryRequester YahooHistoryRequester;

    internal YahooHistory(IClock clock, ILogger logger, IHttpClientFactory httpClientFactory, Instant start, Duration cacheDuration, Frequency frequency)
    {
        Logger = logger;
        Start = start;
        PriceHistoryFrequency = frequency;
        YahooHistoryRequester = new YahooHistoryRequester(logger, httpClientFactory);
        Cache = new ParallelProducerCache<string, Result<ITick[]>>(clock, cacheDuration);
    }

    internal async Task<Result<T[]>> GetTicksAsync<T>(Symbol symbol, CancellationToken ct) where T : ITick
    {
        if (symbol.IsCurrency)
            throw new ArgumentException($"Invalid symbol: '{symbol.Name}'.");
        Type type = typeof(T);
        Frequency frequency = type == typeof(PriceTick) ? PriceHistoryFrequency : Frequency.Daily;
        string key = $"{symbol},{type.Name},{frequency.Name()}";
        try
        {
            Result<ITick[]> result = await Cache.Get(key, () => Produce<T>(symbol.Name, frequency, ct)).ConfigureAwait(false);
            if (result.HasError)
                return result.Error.ToResultError<T[]>();
            return result.Value.Cast<T>().ToArray().ToResult(); // returns a mutable shallow copy
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "History error: {Message}.", e.Message);
            throw;
        }
    }

    private async Task<Result<ITick[]>> Produce<T>(string symbol, Frequency frequency, CancellationToken ct) where T : ITick
    {
        string parm = typeof(T).Name switch
        {
            nameof(PriceTick) => "history",
            nameof(DividendTick) => "div",
            nameof(SplitTick) => "split",
            _ => throw new TypeAccessException("tick")
        };

        const string address = "https://query2.finance.yahoo.com/v7/finance/download/";
        string url = $"{address}{symbol}?period1={(Start == Instant.MinValue ? 0 : Start.ToUnixTimeSeconds())}" +
            $"&period2={Instant.MaxValue.ToUnixTimeSeconds()}&interval=1{frequency.Name()}&events={parm}";

        using HttpResponseMessage response = await YahooHistoryRequester.Request(url, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return Result<ITick[]>.Fail("History not found.");
        response.EnsureSuccessStatusCode();

        try
        {
            using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using StreamReader streamReader = new(stream);
            ITick[] ticks = await streamReader.ToTicks<T>(Logger).ConfigureAwait(false);
            return Result<ITick[]>.Ok(ticks);
        }
        catch (Exception e)
        {
            return Result<ITick[]>.Fail($"{e.GetType().Name}: {e.Message}");
        }
    }
}

using System.Net.Http;
using System.Threading.RateLimiting;

namespace YahooQuotesApi.Utilities;

public sealed class HttpRateLimitingHandler : DelegatingHandler
{
    private static bool IsRunningOnAppVeyor() => Environment.GetEnvironmentVariable("APPVEYOR") == "True";

    private readonly static TokenBucketRateLimiter Limiter = new (new TokenBucketRateLimiterOptions
    {
        TokenLimit = IsRunningOnAppVeyor() ? 1 : int.MaxValue,
        TokensPerPeriod = IsRunningOnAppVeyor() ? 1 : int.MaxValue,
        ReplenishmentPeriod = TimeSpan.FromSeconds(IsRunningOnAppVeyor() ? 30 : 1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = int.MaxValue
    });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using RateLimitLease lease = await Limiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
            throw new HttpRequestException("Rate limit lease not acquired.");
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
  
}

using System.Net.Http;
using System.Threading.RateLimiting;
namespace YahooQuotesApi.Utilities;

public sealed class HttpRateLimitingHandler(RateLimiter limiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using RateLimitLease lease = await limiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
            throw new HttpRequestException("Rate limit lease not acquired.");
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

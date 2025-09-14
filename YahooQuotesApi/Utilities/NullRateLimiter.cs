using System.Threading.RateLimiting;
namespace YahooQuotesApi.Utilities;

public sealed class NullRateLimiter : RateLimiter
{
    public static NullRateLimiter Instance { get; } = new();
    public override RateLimiterStatistics GetStatistics() => new();
    public override TimeSpan? IdleDuration => null;
    protected override RateLimitLease AttemptAcquireCore(int permitCount) =>
        new SuccessfulRateLimitLease();
#pragma warning disable CA2000 // Dispose objects before losing scope
    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        new(new SuccessfulRateLimitLease());
#pragma warning restore CA2000 // Dispose objects before losing scope

    private sealed class SuccessfulRateLimitLease : RateLimitLease
    {
        public override bool IsAcquired => true;
        public override IEnumerable<string> MetadataNames => [];
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}

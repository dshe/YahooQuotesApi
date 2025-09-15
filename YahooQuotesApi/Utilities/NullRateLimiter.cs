using System.Threading.RateLimiting;
namespace YahooQuotesApi.Utilities;

public sealed class NullRateLimiter : RateLimiter
{
    public static NullRateLimiter Instance { get; } = new();
    public override RateLimiterStatistics GetStatistics() => new();
    public override TimeSpan? IdleDuration => null;
    protected override RateLimitLease AttemptAcquireCore(int permitCount) =>
        AcquiredLease.Instance;
    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        new(AcquiredLease.Instance);

    private sealed class AcquiredLease : RateLimitLease
    {
        internal static RateLimitLease Instance { get; } = new AcquiredLease();
        public override bool IsAcquired => true;
        public override IEnumerable<string> MetadataNames => [];
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.RateLimiting;
using YahooQuotesApi.Utilities;
namespace YahooQuotesApi;

public sealed record class YahooQuotesBuilder
{
    public YahooQuotesBuilder() { }

    internal IClock Clock { get; private init; } = SystemClock.Instance;
    internal YahooQuotesBuilder WithClock(IClock clock) =>
        this with { Clock = clock };

    internal ILogger Logger { get; private init; } = NullLogger.Instance;
    public YahooQuotesBuilder WithLogger(ILogger logger) => this with { Logger = logger };
    public YahooQuotesBuilder WithLoggerFactory(ILoggerFactory loggerFactory) =>
        WithLogger(loggerFactory.CreateLogger<YahooQuotes>());

    internal RateLimiter HttpRateLimiter { get; private init; } = NullRateLimiter.Instance;
    public YahooQuotesBuilder WithHttpRateLimiter(RateLimiter httpRateLimiter) =>
        this with { HttpRateLimiter = httpRateLimiter };

    internal Duration SnapshotCacheDuration { get; private init; } = Duration.Zero;
    public YahooQuotesBuilder WithSnapshotCacheDuration(Duration duration) =>
        this with { SnapshotCacheDuration = duration };

    internal Duration HistoryCacheDuration { get; private init; } = Duration.Zero;
    public YahooQuotesBuilder WithHistoryCacheDuration(Duration duration) => 
        this with { HistoryCacheDuration = duration };

    internal Instant HistoryStartDate { get; private init; } = Instant.FromUtc(1970, 1, 1, 0, 0, 0);
    public YahooQuotesBuilder WithHistoryStartDate(Instant start) =>
        this with { HistoryStartDate = start };

    internal bool UseAdjustedClose { get; private init; } = true;
    internal YahooQuotesBuilder DoNotUseAdjustedClose() =>
        this with { UseAdjustedClose = false };

    public YahooQuotes Build() => Services.Build(this);
}

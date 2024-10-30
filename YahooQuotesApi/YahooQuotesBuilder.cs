using Microsoft.Extensions.Logging.Abstractions;
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

    internal string HttpUserAgent { get; private init; } = "";
    public YahooQuotesBuilder WithHttpUserAgent(string httpUserAgent) => 
        this with { HttpUserAgent = httpUserAgent };

    internal bool WithHttpResilience { get; private init; } = true;
    public YahooQuotesBuilder WithoutHttpResilience() =>
        this with { WithHttpResilience = false };

    internal string SnapshotApiVersion { get; private init; } = "v7";
    public YahooQuotesBuilder WithSnapShotApiVersion(string snapshotApiVersion) =>
        this with { SnapshotApiVersion = snapshotApiVersion };

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

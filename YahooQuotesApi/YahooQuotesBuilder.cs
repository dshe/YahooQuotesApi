using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;

namespace YahooQuotesApi;

public sealed record YahooQuotesBuilder
{
    public YahooQuotesBuilder() { }

    /** <summary>for testing</summary> */
    internal YahooQuotesBuilder WithClock(IClock clock) => this with { Clock = clock };
    internal IClock Clock { get; private init; } = SystemClock.Instance;

    public YahooQuotesBuilder WithLogger(ILogger logger) => this with { Logger = logger };
    public YahooQuotesBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory, nameof(loggerFactory));
        return WithLogger(loggerFactory.CreateLogger<YahooQuotesBuilder>());
    }
    internal ILogger Logger { get; private init; } = NullLogger.Instance;

    public YahooQuotesBuilder WithSnapShotApiVersion(string snapshotApiVersion)
    {
        if (string.IsNullOrWhiteSpace(snapshotApiVersion))
            throw new ArgumentException("Invalid argument", nameof(snapshotApiVersion));
        return this with { SnapshotApiVersion = snapshotApiVersion };
    }
    internal string SnapshotApiVersion { get; private init; } = "v7";

    public YahooQuotesBuilder WithHttpUserAgent(string httpUserAgent) =>
        this with { HttpUserAgent = httpUserAgent };
    internal string HttpUserAgent { get; private init; } = "";

    public YahooQuotesBuilder WithHistoryStartDate(Instant start) =>
        this with { HistoryStartDate = start };
    internal Instant HistoryStartDate { get; private init; } = Instant.MinValue;

    public YahooQuotesBuilder WithPriceHistoryFrequency(Frequency frequency) =>
        this with { PriceHistoryFrequency = frequency };
    internal Frequency PriceHistoryFrequency { get; private init; } = Frequency.Daily;

    public YahooQuotesBuilder WithCacheDuration(Duration snapshotCacheDuration, Duration historyCacheDuration)
    {
        if (snapshotCacheDuration > historyCacheDuration)
            throw new ArgumentException("snapshotCacheDuration > historyCacheDuration.");
        return this with { SnapshotCacheDuration = snapshotCacheDuration, HistoryCacheDuration = historyCacheDuration };
    }
    internal Duration SnapshotCacheDuration { get; private init; } = Duration.Zero;
    internal Duration HistoryCacheDuration { get; private init; } = Duration.Zero;

    /** <summary>for testing</summary> */
    internal YahooQuotesBuilder WithNonAdjustedClose() => this with { NonAdjustedClose = true };
    internal bool NonAdjustedClose { get; private init; }

    public YahooQuotes Build() => Services.Build(this);
}

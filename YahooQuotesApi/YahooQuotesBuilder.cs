using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
namespace YahooQuotesApi;

public sealed class YahooQuotesBuilder
{
    internal IClock Clock { get; private set; } =  SystemClock.Instance;
    internal ILogger Logger { get; private set; } = NullLogger.Instance;
    internal Instant HistoryStartDate { get; private set; } = Instant.FromUtc(2020, 1, 1, 0, 0);
    internal Frequency HistoryFrequency { get; private set; } = Frequency.Daily;
    internal Duration HistoryCacheDuration { get; private set; } = Duration.MaxValue;
    internal Duration SnapshotCacheDuration { get; private set; } = Duration.Zero;
    internal bool NonAdjustedClose { get; private set; } // for testing

    internal YahooQuotesBuilder WithClock(IClock clock) // for testing
    {
        Clock = clock;
        return this;
    }

    public YahooQuotesBuilder WithLogger(ILogger logger)
    {
        Logger = logger;
        return this;
    }

    public YahooQuotesBuilder WithHistoryStarting(Instant start)
    {
        HistoryStartDate = start;
        return this;
    }

    public YahooQuotesBuilder WithPriceHistoryFrequency(Frequency frequency)
    {
        HistoryFrequency = frequency;
        return this;
    }

    public YahooQuotesBuilder WithCaching(Duration snapshotDuration, Duration historyDuration)
    {
        if (snapshotDuration > historyDuration)
            throw new ArgumentException("snapshotCacheDuration > historyCacheDuration.");
        SnapshotCacheDuration = snapshotDuration;
        HistoryCacheDuration = historyDuration;
        return this;
    }

    internal YahooQuotesBuilder UsingNonAdjustedClose() // for testing
    {
        NonAdjustedClose = true;
        return this;
    }

    public YahooQuotes Build() => new(this);
}

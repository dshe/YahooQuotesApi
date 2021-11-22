using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
namespace YahooQuotesApi;

public sealed class YahooQuotesBuilder
{
    internal readonly IClock Clock;
    internal readonly ILogger Logger;
    internal Instant HistoryStartDate = Instant.FromUtc(2020, 1, 1, 0, 0);
    internal Frequency HistoryFrequency = Frequency.Daily;
    internal Duration HistoryCacheDuration = Duration.MaxValue;
    internal Duration SnapshotCacheDuration = Duration.Zero;
    internal bool NonAdjustedClose; // used for testing

    public YahooQuotesBuilder() : this(NullLogger.Instance) { }
    public YahooQuotesBuilder(ILogger logger) : this(SystemClock.Instance, logger) { }
    internal YahooQuotesBuilder(IClock clock, ILogger logger)
    {
        Clock = clock;
        Logger = logger;
    }

    public YahooQuotesBuilder HistoryStarting(Instant start)
    {
        HistoryStartDate = start;
        return this;
    }

    public YahooQuotesBuilder SetPriceHistoryFrequency(Frequency frequency)
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

    public YahooQuotesBuilder UseNonAdjustedClose() // useful for testing
    {
        NonAdjustedClose = true;
        return this;
    }

    public YahooQuotes Build() => new(this);
}

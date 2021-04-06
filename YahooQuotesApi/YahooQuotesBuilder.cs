using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using System;

namespace YahooQuotesApi
{
    public sealed class YahooQuotesBuilder
    {
        private readonly IClock Clock;
        private readonly ILogger Logger;
        private Instant HistoryStartDate = Instant.FromUtc(2020, 1, 1, 0, 0);
        private Frequency HistoryFrequency = Frequency.Daily;
        private Duration HistoryCacheDuration = Duration.MaxValue;
        private Duration SnapshotCacheDuration = Duration.Zero;
        private Duration SnapshotDelay = Duration.Zero;
        private bool NonAdjustedClose = false; // used for testing
         
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

        public YahooQuotesBuilder UseNonAdjustedClose()
        {
            NonAdjustedClose = true;
            return this;
        }

        public YahooQuotesBuilder SetSnapshotDelay(Duration snapshotDelay)
        {
            SnapshotDelay = snapshotDelay;
            return this;
        }

        public YahooQuotes Build()
        {
            return new YahooQuotes(
                Clock,
                Logger,
                SnapshotCacheDuration,
                HistoryStartDate,
                HistoryFrequency,
                HistoryCacheDuration,
                SnapshotDelay,
                NonAdjustedClose);
        }
    }
}

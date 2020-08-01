using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using System;

namespace YahooQuotesApi
{
    public sealed class YahooQuotesBuilder
    {
        private readonly ILogger Logger;
        private Instant HistoryStartDate = Instant.FromUtc(2000, 1, 1, 0, 0);
        private Frequency PriceHistoryFrequency = Frequency.Daily;
        private Duration HistoryCacheDuration = Duration.Zero;
        private Duration SnapshotCacheDuration = Duration.Zero;

        public YahooQuotesBuilder() : this(NullLogger.Instance) { }
        public YahooQuotesBuilder(ILogger logger) => Logger = logger;

        public YahooQuotesBuilder HistoryStarting(Instant start)
        {
            HistoryStartDate = start;
            return this;
        }

        public YahooQuotesBuilder SetPriceHistoryFrequency(Frequency frequency)
        {
            PriceHistoryFrequency = frequency;
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

        public YahooQuotes Build()
        {
            return new YahooQuotes(
                Logger,
                HistoryStartDate,
                SnapshotCacheDuration,
                HistoryCacheDuration,
                PriceHistoryFrequency);
        }
    }
}

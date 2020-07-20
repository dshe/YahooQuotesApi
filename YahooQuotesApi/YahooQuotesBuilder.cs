using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using System;

namespace YahooQuotesApi
{
    public sealed class YahooQuotesBuilder
    {
        public static Instant DefaultHistoryStartDate = Instant.FromUtc(2000, 1, 1, 0, 0);
        public static Duration DefaultHistoryCacheDuration = Duration.Zero;
        private Instant HistoryStartDate = DefaultHistoryStartDate;
        private Duration HistoryCacheDuration = DefaultHistoryCacheDuration;
        private readonly ILogger Logger;
        private HistoryFlags HistoryFlags;
        private Frequency PriceHistoryFrequency = Frequency.Daily;
        private string PriceHistoryBaseCurrency = "";

        public YahooQuotesBuilder() : this(NullLogger.Instance) { }
        public YahooQuotesBuilder(ILogger logger) => Logger = logger;

        public YahooQuotesBuilder WithDividendHistory()
        {
            HistoryFlags |= HistoryFlags.DividendHistory;
            return this;
        }
        public YahooQuotesBuilder WithSplitHistory()
        {
            HistoryFlags |= HistoryFlags.SplitHistory;
            return this;
        }
        public YahooQuotesBuilder WithPriceHistory(Frequency frequency = Frequency.Daily, string baseCurrency = "")
        {
            HistoryFlags |= HistoryFlags.PriceHistory;
            PriceHistoryFrequency = frequency;
            if (baseCurrency != "" && baseCurrency.Length != 3)
                throw new ArgumentException(nameof(baseCurrency));
            PriceHistoryBaseCurrency = baseCurrency.ToUpper();
            return this;
        }

        public YahooQuotesBuilder HistoryStarting(Instant start)
        {
            HistoryStartDate = start;
            return this;
        }
        public YahooQuotesBuilder HistoryCache(Duration cacheDuration)
        {
            HistoryCacheDuration = cacheDuration;
            return this;
        }

        public YahooQuotes Build()
        {
            return new YahooQuotes(
                Logger,
                HistoryFlags,
                PriceHistoryFrequency,
                PriceHistoryBaseCurrency,
                HistoryStartDate,
                HistoryCacheDuration);
        }
    }
}

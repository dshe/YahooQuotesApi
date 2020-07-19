using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using System;

namespace YahooQuotesApi
{
    public sealed class YahooQuotesBuilder
    {
        private readonly ILogger Logger;
        private HistoryFlags HistoryFlags;
        private Frequency PriceHistoryFrequency = Frequency.Daily;
        private string PriceHistoryBaseCurrency = "";
        private Instant HistoryStartDate = Instant.FromUtc(2000, 1, 1, 0, 0);
        private Duration HistoryCacheDuration = Duration.Zero;

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

        public YahooQuotesBuilder HistoryStart(Instant start)
        {
            HistoryStartDate = start;
            return this;
        }
        public YahooQuotesBuilder HistoryCache(Duration cacheDuration)
        {
            HistoryCacheDuration = cacheDuration;
            return this;
        }

        public YahooQuotes Build() => 
            new YahooQuotes(
                Logger,
                HistoryFlags, 
                PriceHistoryFrequency,
                PriceHistoryBaseCurrency,
                HistoryStartDate,
                HistoryCacheDuration);
    }
}

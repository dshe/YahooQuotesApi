using NodaTime;
using System;
using System.Threading.Tasks;
using Xunit;

namespace YahooQuotesApi.Tests
{
    public class Examples
    {
        [Fact]
        public async Task Snapshot()
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder().Build();
            
            Security? security = await yahooQuotes.GetAsync("AAPL");
            if (security is null)
                throw new ArgumentException("Unknown symbol: AAPL.");

            Assert.Equal("Apple Inc.", security.LongName);
            Assert.True(security.RegularMarketPrice > 0);
        }

        [Fact]
        public async Task SnapshotWithPriceHistory()
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder()
                .HistoryStarting(Instant.FromUtc(2020, 1, 1, 0, 0))
                .Build();

            Security security = await yahooQuotes.GetAsync("MSFT", HistoryFlags.PriceHistory) ??
                throw new ArgumentException("Unknown symbol: MSFT.");

            Assert.Equal("NasdaqGS", security.FullExchangeName);

            CandleTick[] priceHistory = security.PriceHistory.Value;

            CandleTick tick = priceHistory[0];
            Assert.Equal(new LocalDate(2020, 1, 2), tick.Date);
            Assert.Equal(160.62, tick.Close);
        }

        [Fact]
        public async Task SnapshotWithPriceHistoryInBaseCurrency()
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder()
                .HistoryStarting(Instant.FromUtc(2020, 7, 15, 0, 0))
                .Build();

            Security security = await yahooQuotes
                .GetAsync("TSLA", HistoryFlags.PriceHistory, historyBase: "JPY=X")
                    ?? throw new ArgumentException("Unknown symbol: TSLA.");

            Assert.Equal("Tesla, Inc.", security.ShortName);
            Assert.Equal("USD", security.Currency);

            CandleTick tick = security.PriceHistory.Value[0];
            Assert.Equal(new LocalDate(2020, 7, 15), tick.Date);
            Assert.Equal(309.202, tick.Close); // in USD

            PriceTick tickBase = security.PriceHistoryBase.Value[0];
            Assert.Equal(new LocalDateTime(2020, 7, 15, 16, 0, 0), tickBase.Date.LocalDateTime);
            Assert.Equal(33139, tickBase.Price, 0); // in JPY
        }
    }
}

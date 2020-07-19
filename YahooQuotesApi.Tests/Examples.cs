using NodaTime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class Examples : TestBase
    {
        public Examples(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task Snapshot()
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder(Logger).Build();
            
            IReadOnlyDictionary<string, Security?> securities =
                await yahooQuotes.GetAsync(new[] { "IBM", "MSFT" });

            Security? security = securities["IBM"];

            if (security == null)
                throw new ArgumentException("Unknown symbol: IBM.");

            Assert.Equal("International Business Machines Corporation", security.LongName);
            Assert.True(security.RegularMarketPrice > 10);
        }

        [Fact]
        public async Task History()
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder(Logger)
                .WithPriceHistory()
                .WithDividendHistory()
                .WithSplitHistory()
                .Build();

            Security security = await yahooQuotes.GetAsync("MSFT") ?? throw new ArgumentException();

            Assert.Equal("NasdaqGS", security.FullExchangeName);

            IReadOnlyList<PriceTick>? priceHistory = security.PriceHistory!;
            Assert.Equal(58.28125, priceHistory[0].Close);

            IReadOnlyList<DividendTick>? dividendHistory = security.DividendHistory!;
            Assert.Equal(new LocalDate(2003, 2, 19), dividendHistory[0].Date);
            Assert.Equal(0.08, dividendHistory[0].Dividend);

            IReadOnlyList<SplitTick> splitHistory = security.SplitHistory!;
            Assert.Equal(new LocalDate(2003, 2, 18), splitHistory[0].Date);
            Assert.Equal(1, splitHistory[0].BeforeSplit);
            Assert.Equal(2, splitHistory[0].AfterSplit);
        }

        [Fact]
        public async Task Currency_Rate()
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder(Logger)
                .WithPriceHistory()
                .HistoryStart(Instant.FromUtc(2020, 1, 1, 0, 0))
                .Build();

            Security? security = await yahooQuotes.GetAsync("EURJPY=X");

            Assert.Equal("EUR/JPY", security!.ShortName);

            Assert.Equal(121.970001, security.PriceHistory![0].Close);
        }

        [Fact]
        public async Task History_With_Base_Currency()
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder(Logger)
                .WithPriceHistory(baseCurrency: "JPY")
                .HistoryStart(Instant.FromUtc(2020, 1, 1, 0, 0))
                .Build();

            Security? security = await yahooQuotes.GetAsync("TSLA");

            Assert.Equal("USD", security!.Currency);
            Assert.Equal("Tesla, Inc.", security.ShortName);

            Assert.Equal(46759.61698027081, security.PriceHistory![0].Close);
        }
    }
}

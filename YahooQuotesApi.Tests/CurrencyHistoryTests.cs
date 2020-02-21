using Microsoft.Extensions.Logging;
using MXLogger;
using NodaTime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class CurrencyHistoryTests
    {
        private readonly Action<string> Write;
        private readonly CurrencyHistory CurrencyHistory;
        public CurrencyHistoryTests(ITestOutputHelper output)
        {
            Write = output.WriteLine;
            var loggerFactory = new LoggerFactory().AddMXLogger(Write);
            CurrencyHistory = new CurrencyHistory(loggerFactory.CreateLogger<CurrencyHistory>());
        }

        [Fact]
        public async Task Example()
        {
            List<RateTick>? ticks = await new CurrencyHistory()
                .Period(30)
                .GetRatesAsync("EURJPY=X");

            Assert.NotEmpty(ticks);
        }

        [Fact]
        public async Task BadSymbol()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await CurrencyHistory.GetRatesAsync("X"));
        }

        [Fact]
        public async Task SymbolNotFound()
        {
            Assert.Null(await CurrencyHistory.GetRatesAsync("ABCDEF=X"));
        }

        [Fact]
        public async Task GoodSymbols()
        {
            var date = new LocalDate(2020, 1, 7);

            var rate1 = await GetRate("USDJPY=X", date);
            Assert.Equal(108.61m, rate1);

            var rate2 = await GetRate("EURUSD=X", date); // inverted
            Assert.Equal(1.114m, decimal.Round(rate2, 3));

            var rate3 = await GetRate("EURJPY=X", date);
            Assert.Equal(121.000m, decimal.Round(rate3, 3));

            var EurJpy = rate1 * rate2;
            Assert.Equal(decimal.Round(EurJpy, 3), decimal.Round(rate3, 3));

            // local method
            async Task<decimal> GetRate(string symbol, LocalDate date)
            {
                var list = await CurrencyHistory.Period(date, date).GetRatesAsync(symbol);
                var result = list.Single();
                Assert.Equal(date, result.Date);
                return result.Rate;
            }
        }
    }
}

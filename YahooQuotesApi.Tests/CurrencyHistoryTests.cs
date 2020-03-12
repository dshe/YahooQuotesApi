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
        private readonly ILogger<CurrencyHistory> Logger;
        public CurrencyHistoryTests(ITestOutputHelper output)
        {
            Write = output.WriteLine;
            var loggerFactory = new LoggerFactory().AddMXLogger(Write);
            Logger = loggerFactory.CreateLogger<CurrencyHistory>();
        }

        [Fact]
        public async Task Example()
        {
            List<RateTick>? ticks = await new CurrencyHistory(30, Logger).GetRatesAsync("USDCAD=X");
            Assert.NotEmpty(ticks);
        }

        [Fact]
        public async Task BadSymbol()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await new CurrencyHistory(Logger).GetRatesAsync("X"));
        }

        [Fact]
        public async Task SymbolNotFound()
        {
            Assert.Null(await new CurrencyHistory(Logger).GetRatesAsync("ABCDEF=X"));
        }

        [Fact]
        public async Task GoodSymbols()
        {
            var date = new LocalDate(2020, 1, 7);
            var currencyHistory = new CurrencyHistory(date, date, Logger);

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
                var list = await currencyHistory.GetRatesAsync(symbol);
                var result = list.Single();
                Assert.Equal(date, result.Date);
                return result.Rate;
            }
        }
        [Fact]
        public async Task ManyRates()
        {
            List<RateTick>? ticks = await new CurrencyHistory(Logger).GetRatesAsync("USDMYR=X");
            Assert.NotEmpty(ticks);
            Write($"Days downloaded: {ticks!.Count}.");
        }

        [Fact]
        public async Task ManySymbols()
        {
            Dictionary<string,List<RateTick>?> results = 
                await new CurrencyHistory(30, Logger)
                .GetRatesAsync(new[] { "USDMYR=X", "CADUSD=X", "EURCNY=X"});
            Assert.Equal(3, results.Count);
        }
    }
}

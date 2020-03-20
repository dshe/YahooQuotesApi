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
            IReadOnlyList<RateTick>? ticks = await new CurrencyHistory(Logger).FromDate(new LocalDate(2000,1,1)).GetRatesAsync("USD", "CAD");
            Assert.NotEmpty(ticks);
        }

        [Fact]
        public async Task BadSymbol()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await new CurrencyHistory(Logger).GetRatesAsync("XXX", "USD"));
        }

        [Fact]
        public async Task GoodSymbols()
        {
            var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/London");
            var date = new LocalDate(2020, 1, 7); //.At(new LocalTime(16,0,0)).InZoneStrictly(tz);

            var currencyHistory = new CurrencyHistory(Logger).FromDate(date);

            var rate1 = await GetRate("USD", "JPY", date);
            Assert.Equal(108.61, rate1);

            var rate2 = await GetRate("EUR", "USD", date); // inverted
            Assert.Equal(1.114, Math.Round(rate2, 3));

            var rate3 = await GetRate("EUR", "JPY", date);
            Assert.Equal(121.000, Math.Round(rate3, 3));

            var EurJpy = rate1 * rate2;
            Assert.Equal(Math.Round(EurJpy, 3), Math.Round(rate3, 3));

            // local method
            async Task<double> GetRate(string symbol, string symbolBase, LocalDate date)
            {
                var list = await currencyHistory.GetRatesAsync(symbol, symbolBase);
                var result = list[0];
                Assert.Equal(date, result.Date.InZone(tz).Date);
                return result.Rate;
            }
        }
        [Fact]
        public async Task ManyRates()
        {
            IReadOnlyList<RateTick>? ticks = await new CurrencyHistory(Logger).GetRatesAsync("USD", "MYR");
            Assert.NotEmpty(ticks);
            Write($"Days downloaded: {ticks!.Count}.");
        }

        /*
        [Fact]
        public async Task ManySymbols()
        {
            Dictionary<string,List<RateTick>?> results = 
                await new CurrencyHistory(30, Logger)
                .GetRatesAsync(new[] { "USDMYR=X", "CADUSD=X", "EURCNY=X"});
            Assert.Equal(3, results.Count);
        }
        */
    }
}

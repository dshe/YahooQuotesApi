using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using NodaTime;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MXLogger;

namespace YahooQuotesApi.Tests
{
    public class YahooHistoryTests
    {
        private readonly Action<string> Write;
        private readonly YahooHistory YahooHistory;
        public YahooHistoryTests(ITestOutputHelper output)
        {
            Write = output.WriteLine;
            var loggerFactory = new LoggerFactory().AddMXLogger(Write);
            YahooHistory = new YahooHistory(loggerFactory.CreateLogger<YahooHistory>());
        }

        [Fact]
        public void TestSymbolsArgument()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await YahooHistory.GetPricesAsync(""));
            Assert.ThrowsAsync<ArgumentException>(async () => await YahooHistory.GetPricesAsync(new string[] { }));
            Assert.ThrowsAsync<ArgumentException>(async () => await YahooHistory.GetPricesAsync(new string[] { "" }));
            Assert.ThrowsAsync<ArgumentException>(async () => await YahooHistory.GetPricesAsync(new string[] { "C", "" }));
        }

        [Fact]
        public async Task BadSymbolTest()
        {
            Assert.Null(await new YahooHistory().GetPricesAsync("badSymbol"));
        }

        [Fact]
        public async Task SimpleTest()
        {
            IList<PriceTick>? ticks = await YahooHistory
                .Period(30)
                .GetPricesAsync("C");

            if (ticks == null)
                throw new Exception("Invalid symbol");

            Assert.NotEmpty(ticks);
            Assert.True(ticks[0].Close > 10);
        }

        [Fact]
        public async Task TestSymbols()
        {
            Dictionary<string, List<PriceTick>?> ticks = await YahooHistory.GetPricesAsync(new[] { "C", "badSymbol" });
            Assert.Equal(2 , ticks.Count);
            IList<PriceTick>? tickList = ticks["C"];
            if (tickList == null)
                throw new Exception("Invalid symbol");
            Assert.True(tickList[0].Close > 0);
        }

        [Fact]
        public async Task TestDuplicateSymbols()
        {
            var result = await YahooHistory.GetPricesAsync(new[] { "C", "X", "C" });
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task TestPriceTickTest()
        {
            var symbol = "AAPL";
            var security = await new YahooSnapshot().GetAsync(symbol) ?? throw new Exception($"Invalid symbol: {symbol}.");
            var timeZoneName = security.ExchangeTimezoneName ?? throw new Exception($"TimeZone name not found.");
            var timeZone = timeZoneName.ToTimeZone();

            var date = new LocalDate(2017, 1, 3);
            var instant1 = date.At(new LocalTime(16, 0)).InZoneStrictly(timeZone).ToInstant();
            var instant2 = date.At(new LocalTime(16, 0)).InZoneStrictly(timeZone).ToInstant();

            var ticks = await YahooHistory
                .Period(instant1, instant2)
                .GetPricesAsync(symbol, Frequency.Daily);

            if (ticks == null)
                throw new Exception("Invalid symbol");

            var tick = ticks.Single();

            Assert.Equal(115.800003m, tick.Open);
            Assert.Equal(116.330002m, tick.High);
            Assert.Equal(114.760002m, tick.Low);
            Assert.Equal(116.150002m, tick.Close);
            Assert.Equal(28_781_900, tick.Volume);
        }

        [Fact]
        public async Task TestDividend()
        {
            var symbol = "AAPL";
            var tz = "America/New_York".ToTimeZone();
            var date = new LocalDate(2016, 2, 4);
            var instant1 = date.At(new LocalTime(16, 0)).InZoneStrictly(tz).ToInstant();
            var instant2 = date.At(new LocalTime(16, 0)).InZoneStrictly(tz).ToInstant();

            IList<DividendTick>? list = await YahooHistory
                .Period(instant1, instant2)
                .GetDividendsAsync(symbol);

            var dividend = list.Single().Dividend;

            Assert.Equal(0.52m, dividend);
        }

        [Fact]
        public async Task TestSplit()
        {
            var symbol = "AAPL";
            var tz = "America/New_York".ToTimeZone();
            var date = new LocalDate(2014, 6, 9);
            var instant1 = date.At(new LocalTime(16, 0)).InZoneStrictly(tz).ToInstant();
            var instant2 = date.At(new LocalTime(16, 0)).InZoneStrictly(tz).ToInstant();

            IList<SplitTick>? splits = await YahooHistory
                .Period(instant1, instant2)
                .GetSplitsAsync(symbol);

            Assert.Equal(1, splits[0].BeforeSplit);
            Assert.Equal(7, splits[0].AfterSplit);
        }

        [Fact]
        public async Task TestDates_UK()
        {
            var symbol = "BA.L";
            var security = await new YahooSnapshot().GetAsync(symbol) ?? throw new Exception($"Invalid symbol: {symbol}.");
            var timeZoneName = security.ExchangeTimezoneName ?? throw new Exception($"TimeZone name not found.");
            var timeZone = timeZoneName.ToTimeZone();

            var instant1 = new LocalDateTime(2017, 10, 10, 15, 0).InZoneStrictly(timeZone).ToInstant();
            var instant2 = new LocalDateTime(2017, 10, 12, 15, 0).InZoneStrictly(timeZone).ToInstant();

            var ticks = await YahooHistory
                .Period(instant1, instant2)
                .GetPricesAsync(symbol, Frequency.Daily);
            if (ticks == null)
                throw new Exception("Invalid symbol");

            Assert.Equal(3, ticks.Count());
            Assert.Equal(616.50m, ticks[0].Close);
            Assert.Equal(615.00m, ticks[1].Close);
            Assert.Equal(616.00m, ticks[2].Close);
        }

        [Fact]
        public async Task TestDates_TW()
        {
            var symbol = "2618.TW";
            var security = await new YahooSnapshot().GetAsync(symbol) ?? throw new Exception($"Invalid symbol: {symbol}.");
            var timeZoneName = security.ExchangeTimezoneName ?? throw new Exception($"TimeZone name not found.");
            var timeZone = timeZoneName.ToTimeZone();

            var instant1 = new LocalDateTime(2019, 3, 19, 15, 0).InZoneStrictly(timeZone).ToInstant();
            var instant2 = new LocalDateTime(2019, 3, 21, 15, 0).InZoneStrictly(timeZone).ToInstant();

            var ticks = await YahooHistory
                .Period(instant1, instant2)
                .GetPricesAsync(symbol, Frequency.Daily);
            if (ticks == null)
                throw new Exception("Invalid symbol");

            Assert.Equal(3, ticks.Count());
            Assert.Equal(14.8567m, ticks[0].Close);
            Assert.Equal(14.8082m, ticks[1].Close);
            Assert.Equal(14.8567m, ticks[2].Close);
        }

        [Theory]
        [InlineData("SPY")] // USA
        [InlineData("TD.TO")] // Canada
        [InlineData("BP.L")] // London
        [InlineData("AIR.PA")] // Paris
        [InlineData("AIR.DE")] // Germany
        [InlineData("UNITECH.BO")] // India
        [InlineData("2800.HK")] // Hong Kong
        [InlineData("000001.SS")] // Shanghai
        [InlineData("2448.TW")] // Taiwan
        [InlineData("005930.KS")] // Korea
        [InlineData("BHP.AX")] // Sydney
        [InlineData("7203.T")] // Tokyo
        public async Task TestDates(string symbol)
        {
            var security = await new YahooSnapshot().GetAsync(symbol) ?? throw new Exception($"Invalid symbol: {symbol}.");
            var timeZoneName = security.ExchangeTimezoneName ?? throw new Exception($"TimeZone name not found.");
            var timeZone = timeZoneName.ToTimeZone();

            var date = new LocalDate(2019, 9, 4);
            var instant1 = date.At(new LocalTime(16,0)).InZoneStrictly(timeZone).ToInstant();
            var instant2 = date.At(new LocalTime(16,0)).InZoneStrictly(timeZone).ToInstant();

            var ticks = await YahooHistory
                .Period(instant1, instant2)
                .GetPricesAsync(symbol);

            Assert.Equal(instant1.InUtc().Date, ticks.Single().Date);
        }

        [Fact]
        public async Task TestCurrency()
        {
            var symbol = "EURUSD=X";
            var security = await new YahooSnapshot().GetAsync(symbol) ?? throw new Exception($"Invalid symbol: {symbol}.");

            var timezoneName = security.ExchangeTimezoneName ?? throw new Exception($"Timezone name not found.");
            var timeZone = timezoneName.ToTimeZone();

            var instant1 = new LocalDateTime(2017, 10, 10, 16, 0).InZoneStrictly(timeZone).ToInstant();
            var instant2 = new LocalDateTime(2017, 10, 12, 16, 0).InZoneStrictly(timeZone).ToInstant();


            var ticks = await YahooHistory
                .Period(instant1, instant2)
                .GetPricesAsync(symbol);

            if (ticks == null)
                throw new Exception($"Invalid symbol: {symbol}");

            foreach (var tick in ticks)
                Write($"{tick.Date} {tick.Close}");

            Assert.Equal(3, ticks.Count());
            Assert.Equal(1.174164m, ticks[0].Close);
            Assert.Equal(1.181488m, ticks[1].Close);
            Assert.Equal(1.186549m, ticks[2].Close);
        }

        [Fact]
        public async Task TestFrequency()
        {
            var symbol = "AAPL";
            var timeZone = "America/New_York".ToTimeZone();
            var startDate = new LocalDateTime(2019, 1, 10, 16, 0);

            var ticks1 = await YahooHistory
                .Period(startDate.InZoneStrictly(timeZone).ToInstant())
                .GetPricesAsync(symbol, Frequency.Daily);
            if (ticks1 == null)
                throw new Exception($"Invalid symbol: {symbol}");

            Assert.Equal(new LocalDate(2019, 1, 10), ticks1[0].Date);
            Assert.Equal(new LocalDate(2019, 1, 11), ticks1[1].Date);
            Assert.Equal(152.880005m, ticks1[1].Open);


            var ticks2 = await new YahooHistory()
                .Period(startDate.InZoneStrictly(timeZone).ToInstant())
                .GetPricesAsync(symbol, Frequency.Weekly);
            if (ticks2 == null)
                throw new Exception($"Invalid symbol: {symbol}");

            Assert.Equal(new LocalDate(2019, 1, 7), ticks2[0].Date); // previous Monday
            Assert.Equal(new LocalDate(2019, 1, 14), ticks2[1].Date);
            Assert.Equal(150.850006m, ticks2[1].Open);


            var ticks3 = await YahooHistory
                .Period(startDate.InZoneStrictly(timeZone).ToInstant())
                .GetPricesAsync(symbol, Frequency.Monthly);
            if (ticks3 == null)
                throw new Exception($"Invalid symbol: {symbol}");

            foreach (var tick in ticks3)
                Write($"{tick.Date} {tick.Close}");

            Assert.Equal(new LocalDate(2019, 2, 1), ticks3[0].Date); // next start of month !!!?
            Assert.Equal(new LocalDate(2019, 3, 1), ticks3[1].Date);
            Assert.Equal(174.279999m, ticks3[1].Open);
        }

        private List<string> GetSymbols(int number)
        {
            return File.ReadAllLines(@"..\..\..\symbols.txt")
                .Where(line => !line.StartsWith("#"))
                .Take(number)
                .ToList();
        }

        [Fact]
        public async Task TestManySymbols()
        {
            var symbols = GetSymbols(10);

            //var results = await YahooHistory.Period(10).GetPricesAsync(symbols);
            var results = await new YahooHistory().GetPricesAsync(symbols);
            var invalidSymbols = results.Where(r => r.Value == null).Count();

            // If (message.StartsWith("Call failed. Collection was modified"))
            // this is a bug in Flurl: https://github.com/tmenier/Flurl/issues/398

            Write("");
            Write($"Total Symbols:   {symbols.Count}");
            Write($"Invalid Symbols: {invalidSymbols}");
        }

        [Fact]
        public async Task TestCancellationTimeout()
        {
            var cts = new CancellationTokenSource();
            //cts.CancelAfter(20);

            var task = new YahooHistory().Period(10).GetPricesAsync(GetSymbols(5), Frequency.Daily, cts.Token);

            cts.Cancel();

            await Assert.ThrowsAnyAsync<Exception>(async () => await task);
        }

        [Fact]
        public async Task TestLoggerInjection()
        {
            YahooHistory yahooHistory = new ServiceCollection()
                .AddSingleton<YahooHistory>()
                .BuildServiceProvider()
                .GetRequiredService<YahooHistory>();

            await yahooHistory.GetPricesAsync("C"); // log message should appear in the debug output (when debugging)
        }
    }
}

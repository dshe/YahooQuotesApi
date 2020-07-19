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
using System.Diagnostics;

namespace YahooQuotesApi.Tests
{
    public class YahooHistoryTests : TestBase
    {
        public YahooHistoryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task SingleSecurityTest()
        {
            var yahooQuotes = new YahooQuotesBuilder(Logger)
                .WithPriceHistory()
                .Build();
            var security = await yahooQuotes.GetAsync("IBM");
            Assert.NotEmpty(security?.PriceHistory);
        }

        [Fact]
        public async Task PriceTickTest()
        {
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York")!;
            var instant = new LocalDate(2017, 1, 3)
                .At(new LocalTime(16, 0))
                .InZoneStrictly(timeZone)
                .ToInstant();

            var security = await new YahooQuotesBuilder(Logger)
                .WithPriceHistory()
                .HistoryStart(instant)
                .Build()
                .GetAsync("AAPL");

            var history = security?.PriceHistory;
            var tick = history.First();

            Assert.Equal(115.800003d, tick.Open);
            Assert.Equal(116.330002d, tick.High);
            Assert.Equal(114.760002d, tick.Low);
            Assert.Equal(116.150002d, tick.Close);
            Assert.Equal(28_781_900, tick.Volume);
        }

                /*

        [Fact]
        public async Task TestDividend()
        {
            var symbol = "AAPL";

            // ex-divided date
            var date = new LocalDate(2020, 2, 7);
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");

            // not sure about the timeZone
            var instant = date.AtStartOfDayInZone(timeZone).ToInstant().Minus(Duration.FromSeconds(1));

            IReadOnlyList<DividendTick>? list = await YahooQuotes
                .Starting(instant)
                .GetDividendsAsync(symbol);

            var dividend = list?[0].Dividend;
            var divdate = list?[0].Date;

            Assert.Equal(0.77d, dividend);
            Assert.Equal(date, divdate);
        }

        [Fact]
        public async Task TestSplit()
        {
            var symbol = "AAPL";

            var date = new LocalDate(2014, 6, 9);
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
            var instant = date.AtStartOfDayInZone(timeZone).ToInstant();

            IReadOnlyList<SplitTick>? splits = await YahooQuotes
                .Starting(instant)
                .GetSplitsAsync(symbol);

            Assert.Equal(1, splits[0].BeforeSplit);
            Assert.Equal(7, splits[0].AfterSplit);
        }


        [Fact]
        public async Task TestFrequencyDaily()
        {
            var symbol = "AAPL";
            var startDate = new LocalDate(2019, 1, 10);
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
            var zdt = startDate.At(new LocalTime(16,0,0)).InZoneStrictly(timeZone);

            var ticks = await YahooQuotes
                .Starting(zdt.ToInstant())
                .GetPricesAsync(symbol, Frequency.Daily);

            Assert.Equal(startDate, ticks[0].Date);
            Assert.Equal(152.880005, ticks[1].Open);
        }

        [Fact]
        public async Task TestFrequencyWeekly()
        {
            var symbol = "AAPL";
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
            var startDate = new LocalDate(2019, 1, 7);
            var zdt = startDate.At(new LocalTime(16, 0, 0)).InZoneStrictly(timeZone);

            var ticks = await YahooQuotes
                .Starting(zdt.ToInstant())
                .GetPricesAsync(symbol, Frequency.Weekly);

            Assert.Equal(startDate, ticks[0].Date);
            Assert.Equal(150.850006, ticks[1].Open);
        }

        [Fact]
        public async Task TestFrequencyMonthly()
        {
            var symbol = "AAPL";
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
            var startDate = new LocalDate(2019, 2, 1);
            var zdt = startDate.At(new LocalTime(16, 0, 0)).InZoneStrictly(timeZone);

            var ticks = await YahooQuotes
                .Starting(zdt.ToInstant())
                .GetPricesAsync(symbol, Frequency.Monthly);

            foreach (var tick in ticks)
                Write($"{tick.Date} {tick.Close}");

            Assert.Equal(startDate, ticks[0].Date); // next start of month!
            Assert.Equal(174.279999, ticks[1].Open);
        }

        [Fact]
        public async Task InterpolateHistoryTest()
        {
            var security = await new YahooQuotes().GetAsync("FB");

            var tz = security.ExchangeTimezone;
            var closeTime = security.ExchangeCloseTime ?? throw new Exception("ExchangeCloseTime is null");
            var date = new LocalDate(2020, 1, 10).At(closeTime).InZoneStrictly(tz);

            var history = await new HistoryFlags().GetPricesAsync(security, "JPY");
            var result = history.Single(x => x.Date == date);
            Assert.Equal(23880, Math.Round(result.Close));
        }
        */
    }

    public class TestCache
    {
        private readonly Action<string> Write;
        private readonly AsyncLazyCache<string, List<object>> Cache;
        public TestCache(ITestOutputHelper output)
        {
            Write = output.WriteLine;
            Cache = new AsyncLazyCache<string, List<object>>();
        }

        [Fact]
        public async Task TestCache1()
        {
            await Get("1");
            await Get("2");
            await Get("2");
            await Get("2");
            await Get("3");
            await Get("3");
            await Get("1");
        }

        private async Task<List<object>> Get(string key)
        {
            Write($"getting key {key}");
            return await Cache.Get(key, () => Producer(key));
        }

        private async Task<List<object>> Producer(string key)
        {
            Write($"producing key {key}");
            await Task.Delay(1000);
            var list = new List<object>();
            list.Add(key);
            return list;
        }
    }

}

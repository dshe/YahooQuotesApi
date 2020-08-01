using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using NodaTime;

namespace YahooQuotesApi.Tests
{
    public class HistoryTests : TestBase
    {
        public HistoryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task SingleSecurityTest()
        {
            var yahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(Instant.FromUtc(2020, 1, 1, 0, 0))
                .Build();
            var security = await yahooQuotes.GetAsync("IBM", HistoryFlags.PriceHistory);
            Assert.NotEmpty(security!.PriceHistory);
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
                .HistoryStarting(instant)
                .Build()
                .GetAsync("AAPL", HistoryFlags.PriceHistory);

            var history = security?.PriceHistory;
            var tick = history.First();

            Assert.Equal(115.800003d, tick.Open);
            Assert.Equal(116.330002d, tick.High);
            Assert.Equal(114.760002d, tick.Low);
            Assert.Equal(116.150002d, tick.Close);
            Assert.Equal(28_781_900, tick.Volume);
        }

        [Fact]
        public async Task TestDates_TW()
        {
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Asia/Taipei")!;
            var instant = new LocalDateTime(2019, 3, 19, 15, 0).InZoneStrictly(timeZone).ToInstant();

            var security = await new YahooQuotesBuilder(Logger)
                .HistoryStarting(instant)
                .Build()
                .GetAsync("2618.TW", HistoryFlags.PriceHistory);

            var ticks = security!.PriceHistory ?? throw new NullReferenceException();

            Assert.Equal(14.8567, ticks[0].Close);
            Assert.Equal(14.8082, ticks[1].Close);
            Assert.Equal(14.8567, ticks[2].Close);
        }


        [Fact]
        public async Task TestDividend()
        {
            // ex-divided date
            var date = new LocalDate(2020, 2, 7);
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");

            // not sure about the timeZone
            var instant = date.AtStartOfDayInZone(timeZone!).ToInstant().Minus(Duration.FromSeconds(1));

            var yahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(instant)
                .Build();

            var security = await yahooQuotes.GetAsync("AAPL", HistoryFlags.DividendHistory);
            IReadOnlyList<DividendTick> list = security?.DividendHistory ?? throw new ArgumentException();

            Assert.Equal(0.77d, list[0].Dividend);
            Assert.Equal(date, list[0].Date);
        }

        [Fact]
        public async Task TestSplit()
        {
            var date = new LocalDate(2014, 6, 9);
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
            var instant = date.AtStartOfDayInZone(timeZone!).ToInstant();

            var yahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(instant)
                .Build();

            var security = await yahooQuotes.GetAsync("AAPL", HistoryFlags.SplitHistory);
            IReadOnlyList<SplitTick> splits = security?.SplitHistory ?? throw new ArgumentException();

            Assert.Equal(1, splits[0].BeforeSplit);
            Assert.Equal(7, splits[0].AfterSplit);
        }

        [Fact]
        public async Task TestFrequencyDaily()
        {
            var startDate = new LocalDate(2019, 1, 10);
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
            var zdt = startDate.At(new LocalTime(16, 0, 0)).InZoneStrictly(timeZone!);

            var yahooQuotes = new YahooQuotesBuilder(Logger)
                .SetPriceHistoryFrequency(Frequency.Daily)
                .HistoryStarting(zdt.ToInstant())
                .Build();

            var security = await yahooQuotes.GetAsync("AAPL", HistoryFlags.PriceHistory);
            var ticks = security?.PriceHistory ?? throw new ArgumentException();

            Assert.Equal(zdt, ticks[0].Date);
            Assert.Equal(152.880005, ticks[1].Open);
        }

        [Fact]
        public async Task TestFrequencyWeekly()
        {
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
            var startDate = new LocalDate(2019, 1, 7);
            var zdt = startDate.At(new LocalTime(16, 0, 0)).InZoneStrictly(timeZone!);

            var yahooQuotes = new YahooQuotesBuilder(Logger)
                .SetPriceHistoryFrequency(Frequency.Weekly)
                .HistoryStarting(zdt.ToInstant())
                .Build();

            var security = await yahooQuotes.GetAsync("AAPL", HistoryFlags.PriceHistory);
            var ticks = security?.PriceHistory ?? throw new ArgumentException();

            var instant1 = new LocalDateTime(2019, 1, 7, 16, 0).InZoneStrictly(timeZone!);
            Assert.Equal(instant1, ticks[0].Date); // previous Monday
            var instant2 = new LocalDateTime(2019, 1, 14, 16, 0).InZoneStrictly(timeZone!);
            Assert.Equal(instant2, ticks[1].Date);
            Assert.Equal(150.850006, ticks[1].Open);
        }

        [Fact]
        public async Task TestFrequencyMonthly()
        {
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
            var startDate = new LocalDate(2019, 2, 1);
            var zdt = startDate.At(new LocalTime(16, 0, 0)).InZoneStrictly(timeZone!);

            var yahooQuotes = new YahooQuotesBuilder(Logger)
                .SetPriceHistoryFrequency(Frequency.Monthly)
                .HistoryStarting(zdt.ToInstant())
                .Build();

            var security = await yahooQuotes.GetAsync("AAPL", HistoryFlags.PriceHistory);
            var ticks = security?.PriceHistory ?? throw new ArgumentException();

            foreach (var tick in ticks)
                Write($"{tick.Date} {tick.Close}");

            var zdt1 = new LocalDateTime(2019, 2, 1, 16, 0).InZoneStrictly(timeZone!);
            Assert.Equal(zdt1, ticks[0].Date); // next start of month!
            var zdt2 = new LocalDateTime(2019, 3, 1, 16, 0).InZoneStrictly(timeZone!);
            Assert.Equal(zdt2, ticks[1].Date);
            Assert.Equal(174.279999, ticks[1].Open);
        }
    }
}

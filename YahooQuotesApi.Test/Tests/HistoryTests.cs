using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace YahooQuotesApi.Tests;

public class HistoryTests : TestBase
{
    public HistoryTests(ITestOutputHelper output) : base(output, LogLevel.Debug) { }

    [Fact]
    public async Task SingleSecurityTest()
    {
        var yahooQuotes = new YahooQuotesBuilder(Logger)
            .HistoryStarting(Instant.FromUtc(2020, 1, 1, 0, 0))
            .Build();
        var security = await yahooQuotes.GetAsync("IBM", HistoryFlags.PriceHistory) ?? throw new ArgumentNullException();
        Assert.NotEmpty(security.PriceHistory.Value);
    }

    [Fact]
    public async Task PriceTickTest()
    {
        var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York")!;
        var instant = new LocalDate(2021, 2, 16)
            .At(new LocalTime(16, 0))
            .InZoneStrictly(timeZone)
            .ToInstant();

        var security = await new YahooQuotesBuilder(Logger)
            .HistoryStarting(instant)
            .Build()
            .GetAsync("AAPL", HistoryFlags.PriceHistory) ?? throw new ArgumentException();

        var history = security.PriceHistory;
        var tick = history.Value.First();

        Assert.Equal(133.19d, tick.Close);
        Assert.Equal(80_576_300, tick.Volume);
    }

    [Fact]
    public async Task TestDates_TW()
    {
        var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Asia/Taipei")!;
        var instant = new LocalDateTime(2021, 2, 22, 15, 0).InZoneStrictly(tz).ToInstant();

        var security = await new YahooQuotesBuilder(Logger)
            .HistoryStarting(instant)
            .Build()
            .GetAsync("2618.TW", HistoryFlags.PriceHistory);

        var ticks = security!.PriceHistory.Value;

        Assert.Equal(14.35, ticks[0].Close);
    }

    [Fact]
    public async Task TestDividend()
    {
        var date = new LocalDate(2021, 2, 5);
        var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");

        var zdt = date.AtStartOfDayInZone(tz!);

        var yahooQuotes = new YahooQuotesBuilder(Logger)
            .HistoryStarting(zdt.ToInstant())
            .Build();

        var security = await yahooQuotes.GetAsync("AAPL", HistoryFlags.DividendHistory) ?? throw new ArgumentException();
        var dividends = security.DividendHistory.Value.ToList();
        var dividend = dividends.First();

        Assert.Equal(0.205, dividend.Dividend);
        Assert.Equal(zdt.LocalDateTime.Date, dividend.Date);
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

        var security = await yahooQuotes.GetAsync("AAPL", HistoryFlags.SplitHistory) ?? throw new ArgumentException();
        SplitTick split = security.SplitHistory.Value[0];

        Assert.Equal(1, split.BeforeSplit);
        Assert.Equal(7, split.AfterSplit);
    }

    [Fact]
    public async Task TestFrequencyDaily()
    {
        var startDate = new LocalDate(2019, 1, 10);
        var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
        var zdt = startDate.AtStartOfDayInZone(timeZone!);

        var yahooQuotes = new YahooQuotesBuilder(Logger)
            .SetPriceHistoryFrequency(Frequency.Daily)
            .HistoryStarting(zdt.ToInstant())
            .Build();

        var security = await yahooQuotes.GetAsync("AAPL", HistoryFlags.PriceHistory);
        var ticks = security!.PriceHistory.Value;

        Assert.Equal(zdt.LocalDateTime.Date, ticks[0].Date);
        Assert.Equal(38.13, ticks[0].Open, 1);
    }

    [Fact(Skip = "simplify")]
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
        var ticks = security!.PriceHistory.Value;

        Assert.Equal(startDate, ticks[0].Date); // previous Monday
        Assert.Equal(39.20, ticks[1].Close, 2);
    }

    [Fact(Skip = "simplify")]
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
        var ticks = security!.PriceHistory.Value;

        foreach (var tick in ticks)
            Write($"{tick.Date} {tick.Close}");

        Assert.Equal(startDate, ticks[0].Date); // next start of month!
        Assert.Equal(47.49, ticks[1].Close, 2);
    }
}

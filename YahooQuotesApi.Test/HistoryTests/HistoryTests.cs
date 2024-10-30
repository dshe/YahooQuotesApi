using NodaTime;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace YahooQuotesApi.HistoryTest;

public class HistoryTests : XunitTestBase
{
    private YahooQuotes YahooQuotes { get; }
    public HistoryTests(ITestOutputHelper output) : base(output)
    {
        YahooQuotes = new YahooQuotesBuilder()
            .WithLogger(Logger)
            .WithHistoryStartDate(Instant.FromUtc(2024, 10, 1, 0, 0))
            .DoNotUseAdjustedClose()
            .Build();
    }

    [Theory]
    [InlineData("SPY")]
    [InlineData("XIC.TO")]
    [InlineData("ISF.L")]
    [InlineData("VOW3.DE")]
    [InlineData("2800.HK")]
    [InlineData("^GSPC")]
    [InlineData("EUR=X", "JPY=X")]
    [InlineData("USD=X", "JPY=X")]
    public async Task LatestMarketTimeTest(string symbol, string  baseSymbol = "")
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync(symbol, baseSymbol);
        History history = result.Value;

        Assert.Equal(symbol, history.Symbol.Name);
        DateTimeZone tz = DateTimeZoneProviders.Tzdb[history.ExchangeTimezoneName];

        Write($"Regular: {history.RegularMarketTime.InZone(tz)} {history.RegularMarketPrice}");
        if (history.Ticks.Any())
        {
            Write($"^1:      {history.Ticks[^1].Date.InZone(tz)} {history.Ticks[^1].Close}");
            Write($"^2:      {history.Ticks[^2].Date.InZone(tz)} {history.Ticks[^2].Close}");
        }
        Write($"^1 Base: {history.BaseTicks[^1].Date.InZone(tz)} {history.BaseTicks[^1].Price}");
        Write($"^2 Base: {history.BaseTicks[^2].Date.InZone(tz)} {history.BaseTicks[^2].Price}");
    }

    [Fact]
    public async Task PriceTickTest()
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync("AAPL");
        History history = result.Value;

        DateTimeZone timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(history.ExchangeTimezoneName)!;
        Instant instant = new LocalDate(2024, 10, 1)
            .At(new LocalTime(9, 30)) // start of trading
            .InZoneStrictly(timeZone)
            .ToInstant();

        Tick tick = history.Ticks.First();

        Assert.Equal(instant, tick.Date);
        Assert.Equal(226.21, tick.Close, 2);
        Assert.Equal(63_285_000, tick.Volume);
    }

    [Fact]
    public async Task TestDividend()
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync("ABM");
        History history = result.Value;

        DateTimeZone timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(history.ExchangeTimezoneName)!;
        Instant instant = new LocalDate(2024, 10, 3)
            .At(new LocalTime(9, 30)) // start of trading
            .InZoneStrictly(timeZone)
            .ToInstant();

        Dividend dividend = result.Value.Dividends.First();

        Assert.Equal(instant, dividend.Date);
        Assert.Equal(0.225m, dividend.Amount);
    }

    [Fact]
    public async Task TestSplit()
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync("LRCX");
        History history = result.Value;

        DateTimeZone timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(history.ExchangeTimezoneName)!;
        Instant instant = new LocalDate(2024, 10, 3)
            .At(new LocalTime(9, 30)) // start of trading
            .InZoneStrictly(timeZone)
            .ToInstant();

        Split split = history.Splits.First();

        Assert.Equal(instant, split.Date);
        Assert.Equal(1, split.Denominator);
        Assert.Equal(10, split.Numerator);
    }

}

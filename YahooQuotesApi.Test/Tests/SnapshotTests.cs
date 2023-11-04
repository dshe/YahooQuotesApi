using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests;

public class SnapshotTests : XunitTestBase
{
    private readonly YahooQuotes YahooQuotes;
    public SnapshotTests(ITestOutputHelper output) : base(output, LogLevel.Trace) =>
        YahooQuotes = new YahooQuotesBuilder().WithLogger(Logger).Build();

    [Fact]
    public async Task TestInternationalStocks()
    {
        var symbols = new[]
        {
                "GMEXICOB.MX", // Mexico City - 6
                "TD.TO",  // Canada -5
                "SPY",    // USA -5
                /*
                "PETR4.SA", //Sao_Paulo -3
                "BP.L",   // London 0:
                "AIR.PA", // Paris +1
                "AIR.DE", // Xetra +1
                "AGL.JO", //Johannesburg +2
                "AFLT.ME", // Moscow +3:00
                "UNITECH.BO", // IST (India) +5:30
                "2800.HK", // Hong Kong +8
                "000001.SS", // Shanghai +8
                "2498.TW", // Taiwan +8
                "005930.KS", // Seoul +9
                "7203.T", // Tokyo +9 (Toyota)
                "NAB.AX", // Sydney +10
                "FBU.NZ" // Auckland + 12
                */
            };

        var securities = await YahooQuotes.GetAsync(symbols);

        foreach (var kvp in securities)
        {
            var symbol = kvp.Key;
            var security = kvp.Value ?? throw new Exception($"Unknown Symbol: {symbol}.");
            Assert.Equal(symbol, security.Symbol.Name);

            DateTimeZone exchangeTimeZone = Helpers.GetTimeZone(security.ExchangeTimezoneName);
            LocalTime exchangeCloseTime = Helpers.GetExchangeCloseTimeFromSymbol(security.Symbol);

            Write($"Symbol:            {symbol}");
            Write($"TimeZone:          {exchangeTimeZone}");
            Write($"ExchangeCloseTime: {exchangeCloseTime}");
            Write($"RegularMarketTime: {Instant.FromUnixTimeSeconds(security.RegularMarketTimeSeconds)}");

            var date = new LocalDate(2020, 7, 17)
                .At(exchangeCloseTime)
                .InZoneStrictly(exchangeTimeZone ?? throw new ArgumentException())
                .ToInstant();


            var securityWithHistory = await new YahooQuotesBuilder()
                .WithLogger(Logger)
                .WithHistoryStartDate(date)
                .Build()
                .GetAsync(symbol, Histories.PriceHistory) ?? throw new Exception($"Unknown symbol: {symbol}.");

            var ticks = securityWithHistory.PriceHistoryBase.Value;
            Assert.Equal(date, ticks.First().Date);
        }
    }

    [Fact]
    public async Task TestDates()
    {
        var symbol = "BA.L";
        var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/London")
            ?? throw new TimeZoneNotFoundException();
        var date = new LocalDateTime(2021, 3, 17, 16, 30).InZoneStrictly(timeZone).ToInstant();

        var yahooQuotes = new YahooQuotesBuilder()
            .WithLogger(Logger)
            .WithHistoryStartDate(date)
            .WithNonAdjustedClose()
            .Build();

        var security = await yahooQuotes.GetAsync(symbol, Histories.PriceHistory)
            ?? throw new ArgumentException();

        DateTimeZone exchangeTimeZone = Helpers.GetTimeZone(security.ExchangeTimezoneName);
        Assert.Equal(timeZone, exchangeTimeZone);

        var ticks = security.PriceHistoryBase.Value;
        Assert.Equal(date, ticks[0].Date);
        Assert.Equal(501, ticks[0].Value);
    }
}

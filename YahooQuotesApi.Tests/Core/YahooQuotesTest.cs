using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MXLogger;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class YahooQuotesTest : TestBase
    {
        public YahooQuotesTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task TestDateTime()
        {
            var symbol = "C";
            var yahooQuotes = new YahooQuotesBuilder(Logger).Build();
            Security security = await yahooQuotes.GetAsync(symbol) ?? throw new ArgumentException();

            string? exchangeTimezoneName = security.ExchangeTimezoneName;
            var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(exchangeTimezoneName)!;
            Assert.Equal(tz, security.ExchangeTimezone);

            long? seconds = security.RegularMarketTimeSeconds;
            var instant = Instant.FromUnixTimeSeconds(seconds.GetValueOrDefault());
            var zdt = instant.InZone(tz);
            Assert.Equal(zdt, security.RegularMarketTime);
        }

        [Theory]
        [InlineData("GMEXICOB.MX")] // Mexico City - 6
        [InlineData("TD.TO")]  // Canada -5
        [InlineData("SPY")]    // USA -5
        [InlineData("PETR4.SA")] //Sao_Paulo -3
        [InlineData("BP.L")]   // London 0:
        [InlineData("AIR.PA")] // Paris +1
        [InlineData("AIR.DE")] // Xetra +1
        [InlineData("AGL.JO")] //Johannesburg +2
        [InlineData("AFLT.ME")] // Moscow +3:00
        [InlineData("UNITECH.BO")] // IST +5:30
        [InlineData("2800.HK")] // Hong Kong +8
        [InlineData("000001.SS")] // Shanghai +8
        [InlineData("2448.TW")] // Taiwan +8
        [InlineData("005930.KS")] // Seoul +9
        [InlineData("7203.T")] // Tokyo +9 (Toyota)
        [InlineData("NAB.AX")] // Sydney +10
        [InlineData("FBU.NZ")] // Auckland + 12
        public async Task TestInternationalStocks(string symbol)
        {
            var yahooQuotes = new YahooQuotesBuilder(Logger).Build();
            Security security = await yahooQuotes.GetAsync(symbol) ?? throw new Exception("invalid symbol: " + symbol);
            ZonedDateTime? zdt = security.RegularMarketTime;

            Write($"Symbol:        {symbol}");
            Write($"TimeZone:      {security.ExchangeTimezone}");
            Write($"ZonedDateTime: {zdt}");
        }

        [Fact]
        public async Task TestDates_UK()
        {
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/London");
            var instant = new LocalDateTime(2017, 10, 10, 15, 0).InZoneStrictly(timeZone!).ToInstant();

            var yahooQuotes = new YahooQuotesBuilder(Logger).WithPriceHistory().HistoryStart(instant).Build();
            var security = await yahooQuotes.GetAsync("BA.L");
            var ticks = security?.PriceHistory ?? throw new Exception();

            Assert.Equal(616.50, ticks[0].Close);
            Assert.Equal(615.00, ticks[1].Close);
            Assert.Equal(616.00, ticks[2].Close);
        }
        /*

[Fact]
public async Task TestDates_TW()
{
    var symbol = "2618.TW";

    var instant = new LocalDateTime(2019, 3, 19, 15, 0).InZoneStrictly(timeZone).ToInstant();

    var ticks = await History
        .Starting(instant)
        .GetPricesAsync(symbol);

    Assert.Equal(14.8567, ticks[0].Close);
    Assert.Equal(14.8082, ticks[1].Close);
    Assert.Equal(14.8567, ticks[2].Close);
}

private List<string> GetSymbols(int number) => File.ReadAllLines(@"..\..\..\symbols.txt")
    .Where(line => !line.StartsWith("#"))
    .Take(number)
    .ToList();

[Fact]
public async Task TestManySymbols()
{
    var symbols = GetSymbols(100);
    var securities = await Snapshot.GetAsync(symbols);

    Write($"Total Requested Symbols: {symbols.Count}.");
    var count = securities.Values.Where(v => v == null).Count();
    Write($"Unknown Symbols: {count}.");
    var secs = securities.Where(s => s.Value != null);

    var differentSymbols = secs.Select(s => (s.Key, s.Value?.Symbol)).Where(x => x.Key != x.Symbol).ToList();
    if (differentSymbols.Any())
        throw new Exception($"Currencies changed: {string.Join(", ", differentSymbols)}.");

    //
                count = secs.Where(v => v.Currency == null).Count();
                Write($"Securities with no currency: {count}.");
                count = secs.Where(v => v.ExchangeTimezone == null).Count();
                Write($"Securities with no ExchangeTimezone: {count}.");

                var securities2 = secs.Where(s => s.Currency != null && s.ExchangeTimezone != null);

                var results = await History.FromDays(10).GetPricesAsync(securities2);

                // for large numbers of symbols (thousands)
                // If (message.StartsWith("Call failed. Collection was modified")).
                // This is a bug in Flurl: https://github.com/tmenier/Flurl/issues/366
                // Will probably be fixed in version 3.
        //
}



[Fact]
public async Task TestAllFields()
{
    var loggerFactory = new LoggerFactory().AddMXLogger(Write);
    var Snapshot = new YahooQuotes(loggerFactory);

    var symbol = "C";

    //YahooSnapshot.
    //Security.

    var security1 = await Snapshot.GetAsync(symbol) ?? throw new ArgumentException();
    var s1 = security1.Fields.Select(f => f.Key);
    var n1 = security1.Fields.Count();

    var security2 = await Snapshot.WithFields(Field.LongName).GetAsync(symbol) ?? throw new ArgumentException();
    var s2 = security2.Fields.Select(f => f.Key);
    var n2 = security2.Fields.Count();

    var security3 = await Snapshot.WithFields("longName").GetAsync(symbol) ?? throw new ArgumentException();
    var n3 = security3.Fields.Count();


    var fields = Enum.GetNames(typeof(Field)).ToList();
    fields = fields.Select(f => f.Substring(0, 1).ToLower() + f.Remove(0, 1)).ToList();
    var security4 = await Snapshot.WithFields(fields).GetAsync(symbol) ?? throw new ArgumentException();
    var s4 = security4.Fields.Select(f => f.Key);
    var n4 = security4.Fields.Count();

    var xx = s1.XOr(s2);
    var xy = s1.XOr(s4);

    ;



}
[Fact]
public async Task SingleSecurityTest()
{
    var ticks = await History.GetPricesAsync("IBM");
    Assert.True(ticks.Count > 1);
}

[Fact]
public async Task MultiSecurityTest()
{
    var results = await History.GetPricesAsync(new[] { "C", "IBM" });
    Assert.Equal(2, results.Count);
    Assert.NotEmpty(results[0]);
    Assert.NotEmpty(results[1]);
    Assert.True(results[0].First().Close > 0);
}


[Fact]
public async Task TestDuplicateSecurity()
{
    var security = await Snapshot.GetAsync("C");
    var result = await History.GetPricesAsync(new[] { security, security, null });
    Assert.Single(result);
}

[Fact]
public async Task TestPriceTickTest()
{
    var symbol = "AAPL";
    var date = new LocalDate(2017, 1, 3);
    var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
    var instant = date.At(new LocalTime(16, 0)).InZoneStrictly(timeZone).ToInstant();

    var ticks = await History
        .Starting(instant)
        .GetPricesAsync(symbol);

    var tick = ticks.First();

    Assert.Equal(115.800003d, tick.Open);
    Assert.Equal(116.330002d, tick.High);
    Assert.Equal(114.760002d, tick.Low);
    Assert.Equal(116.150002d, tick.Close);
    Assert.Equal(28_781_900, tick.Volume);
}

[Fact]
public async Task TestDividend()
{
    var symbol = "AAPL";

    // ex-divided date
    var date = new LocalDate(2020, 2, 7);
    var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");

    // not sure about the timeZone
    var instant = date.AtStartOfDayInZone(timeZone).ToInstant().Minus(Duration.FromSeconds(1));

    IReadOnlyList<DividendTick>? list = await History
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

    IReadOnlyList<SplitTick>? splits = await History
        .Starting(instant)
        .GetSplitsAsync(symbol);

    Assert.Equal(1, splits[0].BeforeSplit);
    Assert.Equal(7, splits[0].AfterSplit);
}

[Fact]
public async Task TestDates_UK()
{
    var symbol = "BA.L";

    var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("UK/London");
    var instant = new LocalDateTime(2017, 10, 10, 15, 0).InZoneStrictly(timeZone).ToInstant();

    var ticks = await History
        .Starting(instant)
        .GetPricesAsync(symbol);

    Assert.Equal(616.50, ticks[0].Close);
    Assert.Equal(615.00, ticks[1].Close);
    Assert.Equal(616.00, ticks[2].Close);
}

[Fact]
public async Task TestDates_TW()
{
    var symbol = "2618.TW";

    var instant = new LocalDateTime(2019, 3, 19, 15, 0).InZoneStrictly(timeZone).ToInstant();

    var ticks = await History
        .Starting(instant)
        .GetPricesAsync(symbol);

    Assert.Equal(14.8567, ticks[0].Close);
    Assert.Equal(14.8082, ticks[1].Close);
    Assert.Equal(14.8567, ticks[2].Close);
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
    var security = await Snapshot.GetAsync(symbol);
    var timeZone = security.ExchangeTimezone;

    var zdt = new LocalDate(2019, 9, 4)
        .At(Exchanges.GetCloseTimeFromSymbol(symbol))
        .InZoneStrictly(timeZone);

    var ticks = await History
        .FromDate(zdt.ToInstant())
        .GetPricesAsync(security);

    Assert.Equal(zdt, ticks.First().Date);
}

[Fact]
public async Task TestFrequencyDaily()
{
    var symbol = "AAPL";
    var security = await Snapshot.GetAsync(symbol);
    var timeZone = security.ExchangeTimezone;
    var startDate = new LocalDateTime(2019, 1, 10, 16, 0);
    var zdt = startDate.InZoneStrictly(timeZone);

    var ticks1 = await History
        .FromDate(zdt.ToInstant())
        .WithFrequency(Frequency.Daily)
        .GetPricesAsync(security);

    Assert.Equal(zdt, ticks1[0].Date);
    Assert.Equal(zdt.Plus(Duration.FromDays(1)), ticks1[1].Date);
    Assert.Equal(152.880005, ticks1[1].Open);
}

[Fact]
public async Task TestFrequencyWeekly()
{
    var symbol = "AAPL";
    var security = await Snapshot.GetAsync(symbol);
    var timeZone = security.ExchangeTimezone;
    var startDate = new LocalDateTime(2019, 1, 10, 16, 0);
    var zdt = startDate.InZoneStrictly(timeZone);

    var ticks = await History
        .FromDate(zdt.ToInstant())
        .WithFrequency(Frequency.Weekly)
        .GetPricesAsync(security);

    var instant1 = new LocalDateTime(2019, 1, 7, 16, 0).InZoneStrictly(timeZone);
    Assert.Equal(instant1, ticks[0].Date); // previous Monday
    var instant2 = new LocalDateTime(2019, 1, 14, 16, 0).InZoneStrictly(timeZone);
    Assert.Equal(instant2, ticks[1].Date);
    Assert.Equal(150.850006, ticks[1].Open);
}

[Fact]
public async Task TestFrequencyMonthly()
{
    var symbol = "AAPL";
    var security = await Snapshot.GetAsync(symbol);
    var timeZone = security.ExchangeTimezone;
    var startDate = new LocalDateTime(2019, 1, 10, 16, 0);
    var zdt = startDate.InZoneStrictly(timeZone);

    var ticks = await History
        .FromDate(zdt.ToInstant())
        .WithFrequency(Frequency.Monthly)
        .GetPricesAsync(security);

    foreach (var tick in ticks)
        Write($"{tick.Date} {tick.Close}");

    var zdt1 = new LocalDateTime(2019, 2, 1, 16, 0).InZoneStrictly(timeZone);
    Assert.Equal(zdt1, ticks[0].Date); // next start of month!
    var zdt2 = new LocalDateTime(2019, 3, 1, 16, 0).InZoneStrictly(timeZone);
    Assert.Equal(zdt2, ticks[1].Date);
    Assert.Equal(174.279999, ticks[1].Open);
}

private List<string> GetSymbols(int number) => File.ReadAllLines(@"..\..\..\symbols.txt")
    .Where(line => !line.StartsWith("#"))
    .Take(number)
    .ToList();

[Fact]
public async Task TestManySymbols()
{
    var symbols = GetSymbols(100);
    var securities = await Snapshot.GetAsync(symbols);

    Write($"Total Requested Symbols: {symbols.Count}.");
    var count = securities.Values.Where(v => v == null).Count();
    Write($"Unknown Symbols: {count}.");
    var secs = securities.Where(s => s.Value != null);

    var differentSymbols = secs.Select(s => (s.Key, s.Value?.Symbol)).Where(x => x.Key != x.Symbol).ToList();
    if (differentSymbols.Any())
        throw new Exception($"Currencies changed: {string.Join(", ", differentSymbols)}.");

//
                count = secs.Where(v => v.Currency == null).Count();
                Write($"Securities with no currency: {count}.");
                count = secs.Where(v => v.ExchangeTimezone == null).Count();
                Write($"Securities with no ExchangeTimezone: {count}.");

                var securities2 = secs.Where(s => s.Currency != null && s.ExchangeTimezone != null);

                var results = await History.FromDays(10).GetPricesAsync(securities2);

                // for large numbers of symbols (thousands)
                // If (message.StartsWith("Call failed. Collection was modified")).
                // This is a bug in Flurl: https://github.com/tmenier/Flurl/issues/366
                // Will probably be fixed in version 3.
     //
}

[Fact]
public async Task TestCancellationTimeout()
{
    var symbols = GetSymbols(100);
    var securities = await Snapshot.GetAsync(symbols);

    var cts = new CancellationTokenSource();
    var task = new YahooHistory().FromDays(10).GetPricesAsync(securities.Values, "", cts.Token);

    cts.Cancel();

    await Assert.ThrowsAnyAsync<Exception>(async () => await task);
}
*/
    }
}

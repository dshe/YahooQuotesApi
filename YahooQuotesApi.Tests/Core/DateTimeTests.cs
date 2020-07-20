using Microsoft.Extensions.Logging;
using MXLogger;
using NodaTime;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class DateTimeTests : TestBase
    {
        public DateTimeTests(ITestOutputHelper output) : base(output) { }

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
        [InlineData("UNITECH.BO")] // IST (India) +5:30
        [InlineData("2800.HK")] // Hong Kong +8
        [InlineData("000001.SS")] // Shanghai +8
        [InlineData("2448.TW")] // Taiwan +8
        [InlineData("005930.KS")] // Seoul +9
        [InlineData("7203.T")] // Tokyo +9 (Toyota)
        [InlineData("NAB.AX")] // Sydney +10
        [InlineData("FBU.NZ")] // Auckland + 12
        public async Task TestInternationalStocks(string symbol)
        {
            var security = await new YahooQuotesBuilder(Logger)
                .WithPriceHistory()
                .Build()
                .GetAsync(symbol) ?? throw new Exception($"Unknown symbol: {symbol}.");

            Write($"Symbol:            {symbol}");
            Write($"TimeZone:          {security.ExchangeTimezone}");
            Write($"ExchangeCloseTime: {security.ExchangeCloseTime}");
            Write($"RegularMarketTime: {security.RegularMarketTime}");

            var zdt = new LocalDate(2019, 9, 4)
                .At(security.ExchangeCloseTime.GetValueOrDefault())
                .InZoneStrictly(security.ExchangeTimezone!);

            security = await new YahooQuotesBuilder(Logger)
                .WithPriceHistory()
                .HistoryStarting(zdt.ToInstant())
                .Build()
                .GetAsync(symbol) ?? throw new Exception($"Unknown symbol: {symbol}.");

            var ticks = security.PriceHistory;
            Assert.Equal(zdt, ticks.First().Date);
        }

        [Fact]
        public async Task TestDates_UK()
        {
            var symbol = "BA.L";
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/London");
            var zdt = new LocalDateTime(2017, 10, 10, 16, 30).InZoneStrictly(timeZone!);

            var security = await new YahooQuotesBuilder(Logger)
                .WithPriceHistory()
                .HistoryStarting(zdt.ToInstant())
                .Build()
                .GetAsync(symbol);

            Assert.Equal(timeZone, security!.ExchangeTimezone);

            var ticks = security.PriceHistory ?? throw new Exception();
            Assert.Equal(zdt, ticks[0].Date);
            Assert.Equal(616.50, ticks[0].Close);
            Assert.Equal(615.00, ticks[1].Close);
            Assert.Equal(616.00, ticks[2].Close);
        }

        [Fact]
        public async Task TestManySymbols()
        {
            var symbols = File.ReadAllLines(@"..\..\..\symbols.txt")
                .Where(line => !line.StartsWith("#"))
                .Take(10)
                .ToList();
            //symbols = new List<string>() { "RAIL", "OEDV" };

            var logger = new LoggerFactory().AddMXLogger(Write, LogLevel.Warning).CreateLogger("test");
            var instant = SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromDays(10));

            var securities = await new YahooQuotesBuilder(logger)
                .WithPriceHistory()
                .HistoryStarting(instant)
                .Build()
                .GetAsync(symbols);

            Write($"Requested Symbols: {symbols.Count}.");
            Assert.Equal(symbols.Count, securities.Count);

            var count = securities.Where(s => s.Value == null).Count();
            Write($"Unknown Symbols: {count}.");
            var secs = securities.Where(s => s.Value != null);

            var differentSymbols = secs
                .Select(s => (s.Key, s.Value?.Symbol)).Where(x => x.Key != x.Symbol).ToList();
            if (differentSymbols.Any())
                throw new Exception($"Symbols changed: {string.Join(", ", differentSymbols)}.");

            count = secs.Where(v => v.Value!.Currency == null).Count();
            Write($"Securities with no currency: {count}.");

            count = secs.Where(v => v.Value!.ExchangeTimezone == null).Count();
            Write($"Securities with no ExchangeTimezone: {count}.");

            // for large numbers of symbols (thousands)
            // If (message.StartsWith("Call failed. Collection was modified")).
            // This is a bug in Flurl: https://github.com/tmenier/Flurl/issues/366
            // Will probably be fixed in version 3.
        }

    }
}

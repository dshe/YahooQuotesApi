using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using NodaTime;
using MXLogger;

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

        [Fact]
        public async Task TestInternationalStocks()
        {
            var symbols = new []
            {
                "GMEXICOB.MX", // Mexico City - 6
                "TD.TO",  // Canada -5
                "SPY",    // USA -5
                "PETR4.SA", //Sao_Paulo -3
                "BP.L",   // London 0:
                "AIR.PA", // Paris +1
                "AIR.DE", // Xetra +1
                "AGL.JO", //Johannesburg +2
                "AFLT.ME", // Moscow +3:00
                "UNITECH.BO", // IST (India) +5:30
                "2800.HK", // Hong Kong +8
                "000001.SS", // Shanghai +8
                "2448.TW", // Taiwan +8
                "005930.KS", // Seoul +9
                "7203.T", // Tokyo +9 (Toyota)
                "NAB.AX", // Sydney +10
                "FBU.NZ" // Auckland + 12
            };

            var securities = await new YahooQuotesBuilder(Logger)
                .Build()
                .GetAsync(symbols);

            foreach (var security in securities.Values)
            {
                var symbol = security!.Symbol;
                Write($"Symbol:            {symbol}");
                Write($"TimeZone:          {security.ExchangeTimezone}");
                Write($"ExchangeCloseTime: {security.ExchangeCloseTime}");
                Write($"RegularMarketTime: {security.RegularMarketTime}");

                var zdt = new LocalDate(2020, 7, 17)
                    .At(security.ExchangeCloseTime.GetValueOrDefault())
                    .InZoneStrictly(security.ExchangeTimezone!);

                var securityWithHistory = await new YahooQuotesBuilder(Logger)
                    .HistoryStarting(zdt.ToInstant())
                    .Build()
                    .GetAsync(symbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {symbol}.");

                var ticks = securityWithHistory.PriceHistory;
                Assert.Equal(zdt, ticks.First().Date);
            }
        }

        [Fact]
        public async Task TestDates_UK()
        {
            var symbol = "BA.L";
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/London");
            var zdt = new LocalDateTime(2017, 10, 10, 16, 30).InZoneStrictly(timeZone!);

            var security = await new YahooQuotesBuilder(Logger)
                .HistoryStarting(zdt.ToInstant())
                .Build()
                .GetAsync(symbol, HistoryFlags.PriceHistory);

            Assert.Equal(timeZone, security!.ExchangeTimezone);

            var ticks = security.PriceHistory;
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
                .HistoryStarting(instant)
                .Build()
                .GetAsync(symbols, HistoryFlags.PriceHistory);

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

            // For large numbers of symbols (thousands),
            // if (message.StartsWith("Call failed. Collection was modified")).
            // This is a bug in Flurl: https://github.com/tmenier/Flurl/issues/366
            // which will hopefully be fixed in version 3.
        }
    }
}

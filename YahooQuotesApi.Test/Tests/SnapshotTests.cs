using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class SnapshotTests : TestBase
    {
        private readonly YahooQuotes YahooQuotes;
        public SnapshotTests(ITestOutputHelper output) : base(output, LogLevel.Trace) =>
            YahooQuotes = new YahooQuotesBuilder(Logger).Build();

        [Fact]
        public async Task TestTimeZone()
        {
            Security security = await YahooQuotes.GetAsync("C") ?? throw new ArgumentException();

            string exchangeTimezoneName = security.ExchangeTimezoneName!;
            var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(exchangeTimezoneName);
            Assert.Equal(tz, security.ExchangeTimezone);

            long? seconds = security.RegularMarketTimeSeconds;
            var instant = Instant.FromUnixTimeSeconds(seconds.GetValueOrDefault());
            var zdt = instant.InZone(tz!);
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
                var security = kvp.Value;
                if (security is null)
                    throw new Exception($"Unknown Symbol: {symbol}.");
                Assert.Equal(symbol, security.Symbol);
                Write($"Symbol:            {symbol}");
                Write($"TimeZone:          {security.ExchangeTimezone}");
                Write($"ExchangeCloseTime: {security.ExchangeCloseTime}");
                Write($"RegularMarketTime: {security.RegularMarketTime}");

                var zdt = new LocalDate(2020, 7, 17)
                    .At(security?.ExchangeCloseTime ?? throw new ArgumentException())
                    .InZoneStrictly(security.ExchangeTimezone ?? throw new ArgumentException());

                var securityWithHistory = await new YahooQuotesBuilder(Logger)
                    .HistoryStarting(zdt.ToInstant())
                    .Build()
                    .GetAsync(symbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {symbol}.");

                var ticks = securityWithHistory.PriceHistoryBase.Value;
                Assert.Equal(zdt, ticks.First().Date);
            }
        }

        [Fact]
        public async Task TestDates()
        {
            var symbol = "BA.L";
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/London") 
                ?? throw new TimeZoneNotFoundException();
            var zdt = new LocalDateTime(2021, 3, 17, 16, 30).InZoneStrictly(timeZone);

            var yahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(zdt.ToInstant())
                .UseNonAdjustedClose()
                .Build();

            var security = await yahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory)
                ?? throw new ArgumentException();
            Assert.Equal(timeZone, security.ExchangeTimezone);

            var ticks = security.PriceHistoryBase.Value;
            Assert.Equal(zdt, ticks[0].Date);
            Assert.Equal(501, ticks[0].Price);
        }
    }
}

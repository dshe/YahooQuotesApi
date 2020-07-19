using Microsoft.Extensions.Logging;
using MXLogger;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class YahooSnapshotTest : TestBase
    {
        private readonly YahooQuotes YahooQuotes;
        public YahooSnapshotTest(ITestOutputHelper output) : base(output) =>
            YahooQuotes = new YahooQuotesBuilder(Logger).Build();

        [Fact]
        public async Task SingleQuery()
        {
            var symbol = "C";
            var security = await YahooQuotes.GetAsync(symbol);
            if (security == null)
                throw new ArgumentException();
            Assert.Equal("Citigroup, Inc.", security.ShortName);
        }

        [Fact]
        public async Task MultiQuery()
        {
            IReadOnlyDictionary<string, Security?> securities = await YahooQuotes.GetAsync(new[] { "C", "MSFT" });
            Assert.Equal(2, securities.Count);
            Security? msft = securities["MSFT"];
            if (msft == null)
                throw new ArgumentException();
            Assert.True(msft.RegularMarketVolume > 0);
        }

        [Fact]
        public async Task BadSymbol()
        {
            var security = await YahooQuotes.GetAsync("InvalidSymbol");
            Assert.Null(security);
        }

        [Fact]
        public async Task BadSymbols()
        {
            var snaps = await YahooQuotes.GetAsync(new[] { "MSFT", "InvalidSymbol" });
            Assert.Equal(2, snaps.Count);
            var msft = snaps["MSFT"];
            Assert.NotNull(msft);
            Assert.Null(snaps["InvalidSymbol"]);
        }

        [Fact]
        public async Task TestEmptyList()
        {
            Assert.Empty(await YahooQuotes.GetAsync(new List<string>()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("C", "A", "C ")]
        [InlineData("=X")]
        [InlineData("XX=X")]
        [InlineData("JPYX=X")]
        [InlineData("JPYJPY=X")]
        public async Task TestBadSymbolArgument(params string[] symbols)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await YahooQuotes.GetAsync(symbols));
        }

        [Fact]
        public async Task TestManySymbols()
        {
            var loggerFactory = new LoggerFactory().AddMXLogger(Write, LogLevel.Warning);
            var yahooQuotes = new YahooQuotesBuilder(loggerFactory.CreateLogger("test")).Build();

            var symbols = File.ReadAllLines(@"..\..\..\symbols.txt")
                .Where(line => !line.StartsWith("#"))
                .Take(1000)
                .ToList();

            Write($"requested symbols: {symbols.Count}");

            var securities = await yahooQuotes.GetAsync(symbols);
            Assert.Equal(symbols.Count, securities.Count);

            var validSecurities = securities.Where(kvp => kvp.Value != null).ToList();
            Write($"invalid symbols: {securities.Count - validSecurities.Count}");

            var counted = symbols.Where(s => securities.ContainsKey(s)).Count();
            Write($"missing: {symbols.Count - counted}");
        }

        [Fact]
        public async Task TestValidCurrencyRates()
        {
            await YahooQuotes.GetAsync(new [] { "JPYUSD=X", "USDJPY=X", "EURJPY=X", "JPYEUR=X", "JPY=X" });
        }

        [Fact]
        public async Task TestFields()
        {
            Security security = await YahooQuotes.GetAsync("AAPL") ?? throw new ArgumentException();
            Assert.Equal("Apple Inc.", security["LongName"]); // dynamic type!
            Assert.Equal("Apple Inc.", security.LongName);   // static type
        }

        [Fact]
        public async Task TestNewFields()
        {
            var snap = await YahooQuotes.GetAsync("C") ?? throw new ArgumentNullException();

            var ect = snap["ExchangeCloseTime"];
            Assert.Equal(new LocalTime(16, 0, 0), ect);

            var tz = snap["ExchangeTimezone"];
            Assert.Equal(DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York"), tz);

            var reg = snap["RegularMarketTime"]; // ok, FromUnixTimeSeconds
            Assert.IsType<ZonedDateTime>(reg);

            var div = snap["DividendDate"]; // date, without zone
            Assert.NotNull(div);

            var eaa = snap["EarningsTime"]; // probably already in zone
            Assert.NotNull(eaa);
            var eab = snap["EarningsTimeEnd"];
            Assert.NotNull(eab);
            var eac = snap["EarningsTimeStart"];
            Assert.NotNull(eac);

            var ftd = snap["FirstTradeDate"]; // probably already in zone
            Assert.IsType<LocalDateTime>(ftd);
        }
    }

}

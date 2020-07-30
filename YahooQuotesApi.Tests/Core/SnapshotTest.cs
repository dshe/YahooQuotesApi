using Microsoft.Extensions.Logging;
using MXLogger;
using NodaTime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class SnapshotTest : TestBase
    {
        private readonly YahooQuotes YahooQuotes;
        public SnapshotTest(ITestOutputHelper output) : base(output) =>
            YahooQuotes = new YahooQuotesBuilder(Logger).Build();

        [Fact]
        public async Task SingleQuery()
        {
            var security = await YahooQuotes.GetAsync("C") ?? throw new ArgumentException();
            Assert.Equal("Citigroup, Inc.", security.ShortName);
        }

        [Fact]
        public async Task MultiQuery()
        {
            var symbols = new[] { "C", "MSFT", "ORCL" };
            var securities = await YahooQuotes.GetAsync(symbols);
            Assert.Equal(3, securities.Count);
            var c = securities["C"] ?? throw new ArgumentException();
            Assert.True(c.RegularMarketVolume > 0);
        }

        [Fact]
        public async Task DuplicateTest()
        {
            var securities = await YahooQuotes.GetAsync(new[] { "C", "X", "MSFT", "C" }) ?? throw new ArgumentException();
            Assert.Equal(3, securities.Count);
        }

        [Fact]
        public async Task TestCancellation()
        {
            var ct = new System.Threading.CancellationToken(true);
            var task = YahooQuotes.GetAsync("IBM", ct: ct);
            var e = await Assert.ThrowsAnyAsync<Exception>(async () => await task);
            Assert.True(e.InnerException is OperationCanceledException);
        }

        [Fact]
        public async Task TestEmptyList()
        {
            Assert.Empty(await YahooQuotes.GetAsync(new List<string>()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("C", "A", "C ")]
        //[InlineData("=X")]
        //[InlineData("JPYX=X")]
        [InlineData("JPY=X")]
        public async Task TestInvalidSymbols(params string[] symbols)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await YahooQuotes.GetAsync(symbols));
        }

        [Fact]
        public async Task UnknownSymbols()
        {
            var snaps = await YahooQuotes.GetAsync(new[] { "MSFT", "UnknownSymbol" });
            Assert.Equal(2, snaps.Count);
            Assert.NotNull(snaps["MSFT"]);
            Assert.Null(snaps["UnknownSymbol"]);
        }

        [Fact]
        public async Task TestFieldAccess()
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

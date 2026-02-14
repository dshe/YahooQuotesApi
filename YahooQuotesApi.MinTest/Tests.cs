using NodaTime;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
namespace YahooQuotesApi.MinTest;

public class Tests(ITestOutputHelper output) : XunitTestBase(output)
{
    [Fact]
    public async Task TestTest()
    {
        //const string symbol = "SPY";
        //const string symbol = "^SP400TR";
        const string symbol = "GC=F";

        Snapshot? snapshot = await YahooQuotes.GetSnapshotAsync(symbol, TestContext.Current.CancellationToken);
        Result<History> result = await YahooQuotes.GetHistoryAsync(symbol, "EUR=X", TestContext.Current.CancellationToken);
        ;
    }

    [Fact]
    public async Task SnapshotTest()
    {
        Dictionary<string, Snapshot?> snapshots = await YahooQuotes.GetSnapshotAsync(["AAPL", "BP.L", "USDJPY=X"], TestContext.Current.CancellationToken);
        Snapshot snapshot = snapshots["BP.L"] ?? throw new ArgumentException("Unknown symbol.");

        Assert.Equal("BP p.l.c.", snapshot.LongName);
        Assert.Equal("GBP=X", snapshot.Currency.Name);
        Assert.True(snapshot.RegularMarketPrice > 0);
    }

    [Fact]
    public async Task HistoryTest()
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync("MSFT", "", TestContext.Current.CancellationToken);
        History history = result.Value;

        Assert.Equal("Microsoft Corporation", history.LongName);
        Assert.Equal("USD=X", history.Currency.Name);
        Assert.Equal("America/New_York", history.ExchangeTimezoneName);
        DateTimeZone tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(history.ExchangeTimezoneName) ??
            throw new ArgumentNullException("Unknown timezone");

        ImmutableArray<Tick> ticks = history.Ticks;
        Tick firstTick = ticks[0];
        ZonedDateTime zdt = firstTick.Date.InZone(tz);
        // Note that tick time is market open of 9:30.
        Assert.Equal(new LocalDateTime(2024, 10, 1, 9, 30, 0), zdt.LocalDateTime);
        Assert.Equal(420.69, firstTick.Close, 2); // in USD
    }

    [Fact]
    public async Task ModulesTest()
    {
        Result<JsonProperty[]> result = await YahooQuotes.GetModulesAsync("TSLA", ["assetProfile", "defaultKeyStatistics"], TestContext.Current.CancellationToken);
        Assert.True(result.HasValue);
        JsonProperty[] properties = result.Value;
        Assert.NotEmpty(properties);
    }
}

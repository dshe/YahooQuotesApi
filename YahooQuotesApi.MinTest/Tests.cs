using NodaTime;
using System.Collections.Immutable;
using System.Text.Json;
namespace YahooQuotesApi.MinTest;

public class Tests(ITestOutputHelper output) : XunitTestBase(output)
{
    [Fact]
    public async Task SnapshotTest()
    {
        Dictionary<string, Snapshot?> snapshots = await YahooQuotes.GetSnapshotAsync(["AAPL", "BP.L", "USDJPY=X"]);

        Snapshot snapshot = snapshots["BP.L"] ?? throw new ArgumentException("Unknown symbol.");

        Assert.Equal("BP p.l.c.", snapshot.LongName);
        Assert.Equal("GBP=X", snapshot.Currency.Name);
        Assert.True(snapshot.RegularMarketPrice > 0);
    }

    [Fact(Skip = "Too Many Requests")]
    public async Task HistoryTest()
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync("MSFT");
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

    [Fact(Skip = "Too Many Requests")]
    public async Task ModulesTest()
    {
        Result<JsonProperty[]> result = await YahooQuotes.GetModulesAsync("TSLA", ["assetProfile", "defaultKeyStatistics"]);
        Assert.True(result.HasValue);
        JsonProperty[] properties = result.Value;
        Assert.NotEmpty(properties);
    }
}

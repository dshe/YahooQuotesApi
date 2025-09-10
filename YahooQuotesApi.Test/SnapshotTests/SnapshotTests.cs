namespace YahooQuotesApi.SnapshotTest;

public class SnapshotTests : XunitTestBase
{
    YahooQuotes YahooQuotes { get; }
    public SnapshotTests(ITestOutputHelper output) : base(output)
    {
        YahooQuotes = new YahooQuotesBuilder()
            .WithLogger(Logger)
            .Build();
    }

    [Fact]
    public async Task UnknownSymbolTest()
    {
        Snapshot? snapshot = await YahooQuotes.GetSnapshotAsync("UNKNOWN_SYMBOL");
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task StockTest()
    {
        Snapshot snapshot = await YahooQuotes.GetSnapshotAsync("MSFT") 
            ?? throw new ArgumentNullException("Snapshot is null");
        Assert.Equal("MSFT", snapshot.Symbol.Name);
        Assert.Equal("USD=X", snapshot.Currency.Name);
        Assert.True(snapshot.RegularMarketPrice > 0); // may be null
        Write($"Price:    {snapshot.RegularMarketPrice}");
    }

    [Fact]
    public async Task CurrencyTest()
    {
        Snapshot snapshot = await YahooQuotes.GetSnapshotAsync("USDJPY=X")
            ?? throw new ArgumentNullException("Snapshot is null");
        Assert.Equal("USDJPY=X", snapshot.Symbol.Name);
        Assert.Equal("JPY=X", snapshot.Currency.Name);
        Assert.True(snapshot.RegularMarketPrice > 0);
        Write($"Price:    {snapshot.RegularMarketPrice}");
    }

    [Fact]
    public async Task SymbolsTest()
    {
        Dictionary<string, Snapshot?> snapshots = await YahooQuotes.GetSnapshotAsync(["MSFT", "USDJPY=X", "UNKNOWN_SYMBOL", "MSFT"]);
        Assert.Equal(3, snapshots.Count);
        Snapshot? msft = snapshots["MSFT"];
        Assert.Equal("MSFT", msft?.Symbol.Name);
    }

}


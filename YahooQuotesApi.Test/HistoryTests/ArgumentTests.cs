namespace YahooQuotesApi.HistoryTest;

public class ArgumentTests : XunitTestBase
{
    private readonly YahooQuotes YahooQuotes;
    public ArgumentTests(ITestOutputHelper output) : base(output, LogLevel.Trace) =>
        YahooQuotes = new YahooQuotesBuilder().WithLogger(Logger).Build();

    [Fact]
    public async Task NullAndEmptySymbolTest()
    {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => YahooQuotes.GetHistoryAsync((string)null));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => YahooQuotes.GetHistoryAsync(""));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await YahooQuotes.GetHistoryAsync((new string[1])));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await YahooQuotes.GetHistoryAsync((string[])null));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => YahooQuotes.GetHistoryAsync([null]));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => YahooQuotes.GetHistoryAsync([""]));
#pragma warning restore CS8625
#pragma warning restore CS8600
        _ = await YahooQuotes.GetHistoryAsync(Array.Empty<string>());
    }

    [Theory]
    [InlineData("C", "")]
    [InlineData("C", "C")]
    [InlineData("C", "USD=X")]
    [InlineData("C", "JPY=X")]
    [InlineData("C", "SPY")]
    [InlineData("C", "1306.T")]
    [InlineData("1306.T", "JPY=X")]
    [InlineData("1306.T", "USD=X")]
    [InlineData("1306.T", "SPY")]
    [InlineData("USD=X", "USD=X")]
    [InlineData("CAD=X", "USD=X")]
    [InlineData("CHF=X", "JPY=X")]
    [InlineData("CHF=X", "CHF=X")]
    [InlineData("CHF=X", "X")]
    public async Task OkTest(string symbol, string baseSymbol)
    {
        await YahooQuotes.GetHistoryAsync(symbol, baseSymbol);
    }

    [Theory]
    [InlineData("C ")] // invalid symbol
    [InlineData("X", "C ")] // invalid base
    [InlineData("JPYUSD=X")] // invalid symbol
    [InlineData("USDJPY=X", "SPY")] // invalid symbol
    [InlineData("USDJPY=X", "EUR=X")] // invalid symbol
    [InlineData("X", "JPYUSD=X")] // invalid symbol
    [InlineData("USDJPY=X", "USDEUR=X")]
    [InlineData("USD=X", "USDEUR=X")]
    [InlineData("EUR=X")] // no base
    public async Task InvalidSymbolTest(string symbol, string baseSymbol = "")
    {
        Exception exception = await Assert.ThrowsAnyAsync<Exception>(async () => await YahooQuotes.GetHistoryAsync(symbol, baseSymbol));
        Write(exception.Message.ToString());
    }

    [Theory]
    [InlineData("UnknownSymbol", "")]
    [InlineData("UnknownSymbol", "EUR=X")]
    [InlineData("UnknownSymbol", "SPY")]
    [InlineData("XXX=X", "SPY")]
    [InlineData("XXX=X", "EUR=X")]
    [InlineData("X", "UnknownSymbol")]
    [InlineData("X", "XXX=X")]
    [InlineData("USD=X", "XXX=X")]
    public async Task UnknownSymbolTest(string symbol, string baseSymbol = "")
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync(symbol, baseSymbol);
        Assert.False(result.HasValue);
        Assert.True(result.HasError);
        Assert.False(result.IsUndefined);
        Write(result.ToString());
        Assert.Contains("No data found", result.ToString());
    }

    [Theory]
    [InlineData("ARM.L")]
    public async Task NoHistoryTest(string symbol, string baseSymbol = "")
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync(symbol, baseSymbol);
        Assert.True(result.HasError);
        Assert.Contains("No 'timestamp' property found", result.Error.Message);
    }

    [Theory]
    [InlineData("RUS.PA")]
    public async Task PartialHistoryTest(string symbol, string baseSymbol = "")
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync(symbol, baseSymbol);
        History history = result.Value;
        Assert.NotEmpty(history.Ticks);
        Assert.NotEmpty(history.BaseTicks);
    }

    [Theory]
    [InlineData("CSNKY.MI")]
    public async Task WildTest(string symbol, string baseSymbol = "")
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync(symbol, baseSymbol);
        History history = result.Value;
        Assert.NotEmpty(history.Ticks);
        Assert.NotEmpty(history.BaseTicks);
    }

    [Fact]
    public async Task OkSymbolsTest()
    {
        Dictionary<string, Result<History>> results = await YahooQuotes.GetHistoryAsync([ "AAPL", "MSFT", "JPY=X" ], "USD=X");
        Assert.Equal(3, results.Count);
        Result<History> result = results["JPY=X"];
        Assert.True(result.HasValue);
        Assert.False(result.HasError);
        Assert.False(result.IsUndefined);
    }

    [Fact]
    public async Task TestFieldAccess()
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync("AAPL");
        Assert.Equal("Apple Inc.", result.Value.LongName);  // static type
    }

    [Fact]
    public async Task IgnoreDuplicateTest()
    {
        string[] symbols = ["C", "X", "MSFT", "C"];
        var results = await YahooQuotes.GetHistoryAsync(symbols);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task CancellationTest()
    {
        var ct = new CancellationToken(true);
        var task1 = YahooQuotes.GetHistoryAsync("IBM", ct: ct);
        var e1 = await Assert.ThrowsAnyAsync<Exception>(() => task1);
        Assert.True(e1 is OperationCanceledException);
    }
}

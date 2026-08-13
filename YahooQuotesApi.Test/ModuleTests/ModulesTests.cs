using System.Text.Json;
namespace YahooQuotesApi.ModuleTest;

public class ModulesTests : XunitTestBase
{
    private readonly YahooQuotes YahooQuotes;
    public ModulesTests(ITestOutputHelper output) : base(output)
    {
        YahooQuotes = new YahooQuotesBuilder()
            .WithLogger(Logger)
            .Build();
    }

    [Theory]
    [InlineData("F", "Price")]
    [InlineData("TSLA", "assetProfile", "defaultKeyStatistics")]
    [InlineData("MSFT", "Price", "calendarEvents", "balanceSheetHistoryQuarterly")]
    public async Task ValidMultiModules(string symbol, params string[] moduleNamesRequested)
    {
        Result<JsonProperty[]> result = await YahooQuotes.GetModulesAsync(symbol, moduleNamesRequested, TestContext.Current.CancellationToken);
        List<string> except = result.Value.Select(m => m.Name).Except(moduleNamesRequested, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Empty(except);
    }

    [Fact]
    public async Task InvalidSymbolName()
    {
        Result<JsonElement> result = await YahooQuotes.GetModuleAsync("InvalidSymbol", "Price", TestContext.Current.CancellationToken);
        Assert.Equal("Quote not found for symbol: INVALIDSYMBOL", result.Error.Message);

        Result<JsonProperty[]> results = await YahooQuotes.GetModulesAsync("InvalidSymbol", ["Price", "balanceSheetHistoryQuarterly"], TestContext.Current.CancellationToken);
        Assert.Equal("Quote not found for symbol: INVALIDSYMBOL", results.Error.Message);

        Result<JsonProperty[]> results2 = await YahooQuotes.GetModulesAsync("InvalidSymbol", ["InvalidModuleName1", "InvalidModuleName2"], TestContext.Current.CancellationToken);
        Assert.Equal("Quote not found for symbol: INVALIDSYMBOL", results2.Error.Message);
    }

    [Fact]
    public async Task AllInvalidModuleName()
    {
        // no valid module names indicated
        Result<JsonElement> result = await YahooQuotes.GetModuleAsync("IBM", "InvalidModuleName", TestContext.Current.CancellationToken);
        Assert.Equal("No fundamentals data found for symbol: IBM", result.Error.Message);

        // no valid mdoule names indicated
        Result<JsonProperty[]> results = await YahooQuotes.GetModulesAsync("IBM", ["InvalidModuleName1", "InvalidModuleName2"], TestContext.Current.CancellationToken);
        Assert.Equal("No fundamentals data found for symbol: IBM", results.Error.Message);

        // valid module name(s) with invalid module name(s)
        results = await YahooQuotes.GetModulesAsync("IBM", ["InvalidModuleName1", "price", "InvalidModuleName2"], TestContext.Current.CancellationToken);
        Assert.Equal("Invalid module(s): 'InvalidModuleName1, InvalidModuleName2'.", results.Error.Message);
    }

    [Fact]
    public async Task SomeInvalidModuleName()
    {
        Result<JsonProperty[]> results = await YahooQuotes.GetModulesAsync("IBM", ["Price", "InvalidModuleName"], TestContext.Current.CancellationToken);
        Assert.Equal("Invalid module(s): 'InvalidModuleName'.", results.Error.Message);

        results = await YahooQuotes.GetModulesAsync("IBM", ["InvalidModuleName", "Price"], TestContext.Current.CancellationToken);
        Assert.Equal("Invalid module(s): 'InvalidModuleName'.", results.Error.Message);

        results = await YahooQuotes.GetModulesAsync("IBM", ["InvalidModuleName1", "Price", "InvalidModuleName2"], TestContext.Current.CancellationToken);
        Assert.Equal("Invalid module(s): 'InvalidModuleName1, InvalidModuleName2'.", results.Error.Message);
    }

    [Fact]
    public async Task DuplicateModuleName()
    {
        Result<JsonProperty[]> results = await YahooQuotes.GetModulesAsync("IBM", ["balanceSheetHistoryQuarterly", "Price", "Price", "balanceSheetHistoryQuarterly"], TestContext.Current.CancellationToken);
        Assert.Equal("Duplicate module(s): 'balanceSheetHistoryQuarterly, Price'.", results.Error.Message);
    }

    [Fact]
    public async Task Example()
    {
        Result<JsonProperty[]> result = await YahooQuotes.GetModulesAsync("TSLA", [ "assetProfile", "defaultKeyStatistics" ], TestContext.Current.CancellationToken);
        Assert.True(result.HasValue);
        JsonProperty[] properties = result.Value;
        Assert.NotEmpty(properties);

        var XX = await GetFloatShares("tsla", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TestGetFloatShares()
    {
        long shares = await GetFloatShares("TSLA", TestContext.Current.CancellationToken);
        Assert.True(shares > 0);
    }

    public async Task<long> GetFloatShares(string symbol, CancellationToken ct)
    {
        Result<JsonElement> result = await YahooQuotes.GetModuleAsync(symbol, "defaultKeyStatistics", ct);
        if (result.HasError)
            throw new InvalidOperationException(result.Error.Message);
        JsonElement value = result.Value.GetProperty("floatShares").GetProperty("raw");
        return value.GetInt64();
    }
}

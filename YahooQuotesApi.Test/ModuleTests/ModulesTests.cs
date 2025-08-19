using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
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
    [InlineData("IBM", "Price")]
    [InlineData("X", "AssetProfile")]
    public async Task ValidSingleModule(string symbol, string moduleName)
    {
        Result<JsonProperty> result = await YahooQuotes.GetModuleAsync(symbol, moduleName);
        Assert.Equal(moduleName, result.Value.Name, true);
    }

    [Theory]
    [InlineData("X", "Price")]
    [InlineData("TSLA", "assetProfile", "defaultKeyStatistics")]
    [InlineData("MSFT", "Price", "calendarEvents", "balanceSheetHistoryQuarterly")]
    public async Task ValidMultiModules(string symbol, params string[] moduleNamesRequested)
    {
        Result<JsonProperty[]> result = await YahooQuotes.GetModulesAsync(symbol, moduleNamesRequested);
        var except = result.Value.Select(m => m.Name).Except(moduleNamesRequested, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Empty(except);
    }

    [Fact]
    public async Task InvalidSymbolName()
    {
        var result = await YahooQuotes.GetModuleAsync("InvalidSymbol", "Price");
        Assert.Equal("Quote not found for symbol: INVALIDSYMBOL", result.Error.Message);

        var results = await YahooQuotes.GetModulesAsync("InvalidSymbol", ["Price", "balanceSheetHistoryQuarterly"]);
        Assert.Equal("Quote not found for symbol: INVALIDSYMBOL", results.Error.Message);

        var results2 = await YahooQuotes.GetModulesAsync("InvalidSymbol", ["InvalidModuleName1", "InvalidModuleName2"]);
        Assert.Equal("Quote not found for symbol: INVALIDSYMBOL", results2.Error.Message);
    }

    [Fact]
    public async Task AllInvalidModuleName()
    {
        // no valid module names indicated
        var result = await YahooQuotes.GetModuleAsync("IBM", "InvalidModuleName");
        Assert.Equal("No fundamentals data found for symbol: IBM", result.Error.Message);

        // no valid mdoule names indicated
        var results = await YahooQuotes.GetModulesAsync("IBM", ["InvalidModuleName1", "InvalidModuleName2"]);
        Assert.Equal("No fundamentals data found for symbol: IBM", results.Error.Message);

        // valid module name(s) with invalid module name(s)
        results = await YahooQuotes.GetModulesAsync("IBM", ["InvalidModuleName1", "price", "InvalidModuleName2"]);
        Assert.Equal("Invalid module(s): 'InvalidModuleName1, InvalidModuleName2'.", results.Error.Message);
    }

    [Fact]
    public async Task SomeInvalidModuleName()
    {
        var results = await YahooQuotes.GetModulesAsync("IBM", ["Price", "InvalidModuleName"]);
        Assert.Equal("Invalid module(s): 'InvalidModuleName'.", results.Error.Message);

        results = await YahooQuotes.GetModulesAsync("IBM", ["InvalidModuleName", "Price"]);
        Assert.Equal("Invalid module(s): 'InvalidModuleName'.", results.Error.Message);

        results = await YahooQuotes.GetModulesAsync("IBM", ["InvalidModuleName1", "Price", "InvalidModuleName2"]);
        Assert.Equal("Invalid module(s): 'InvalidModuleName1, InvalidModuleName2'.", results.Error.Message);
    }

    [Fact]
    public async Task DuplicateModuleName()
    {
        var results = await YahooQuotes.GetModulesAsync("IBM", ["balanceSheetHistoryQuarterly", "Price", "Price", "balanceSheetHistoryQuarterly"]);
        Assert.Equal("Duplicate module(s): 'balanceSheetHistoryQuarterly, Price'.", results.Error.Message);
    }

    [Fact]
    public async Task Example()
    {
        Result<JsonProperty[]> result = await YahooQuotes.GetModulesAsync("TSLA", [ "assetProfile", "defaultKeyStatistics" ]);
        Assert.True(result.HasValue);
        JsonProperty[] properties = result.Value;
        Assert.NotEmpty(properties);
    }
}

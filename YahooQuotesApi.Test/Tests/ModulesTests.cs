using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests;

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
        Assert.Equal("Quote not found for ticker symbol: INVALIDSYMBOL", result.Error.Message);

        var results = await YahooQuotes.GetModulesAsync("InvalidSymbol", new[] { "Price", "balanceSheetHistoryQuarterly" });
        Assert.Equal("Quote not found for ticker symbol: INVALIDSYMBOL", results.Error.Message);

        results = await YahooQuotes.GetModulesAsync("InvalidSymbol", new[] { "InvalidModuleName1", "InvalidModuleName2" });
        Assert.Equal("Quote not found for ticker symbol: INVALIDSYMBOL", results.Error.Message);
    }

    [Fact]
    public async Task AllInvalidModuleName()
    {
        var result = await YahooQuotes.GetModuleAsync("IBM", "InvalidModuleName");
        Assert.Equal("No fundamentals data found for any of the summaryTypes=", result.Error.Message);

        var results = await YahooQuotes.GetModulesAsync("IBM", new[] { "InvalidModuleName1", "InvalidModuleName2" });
        Assert.Equal("No fundamentals data found for any of the summaryTypes=", results.Error.Message);
    }

    [Fact]
    public async Task SomeInvalidModuleName()
    {
        var results = await YahooQuotes.GetModulesAsync("IBM", new[] { "Price", "InvalidModuleName" });
        Assert.Equal("Invalid module(s): 'InvalidModuleName'.", results.Error.Message);

        results = await YahooQuotes.GetModulesAsync("IBM", new[] { "InvalidModuleName", "Price" });
        Assert.Equal("Invalid module(s): 'InvalidModuleName'.", results.Error.Message);

        results = await YahooQuotes.GetModulesAsync("IBM", new[] { "InvalidModuleName1", "Price", "InvalidModuleName2" });
        Assert.Equal("Invalid module(s): 'InvalidModuleName1, InvalidModuleName2'.", results.Error.Message);
    }

    [Fact]
    public async Task DuplicateModuleName()
    {
        var results = await YahooQuotes.GetModulesAsync("IBM", new[] { "balanceSheetHistoryQuarterly", "Price", "Price", "balanceSheetHistoryQuarterly" });
        Assert.Equal("Duplicate module(s): 'balanceSheetHistoryQuarterly, Price'.", results.Error.Message);
    }

    [Fact]
    public async Task Example()
    {
        Result<JsonProperty[]> result = await YahooQuotes.GetModulesAsync("TSLA", new[] { "assetProfile", "defaultKeyStatistics" });
        Assert.True(result.HasValue);
        JsonProperty[] properties = result.Value;
        Assert.NotEmpty(properties);
    }
}

using Microsoft.Extensions.Logging;
using NodaTime;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace YahooQuotesApi.HistoryTest;

public class HistoryBaseTests : XunitTestBase
{
    YahooQuotes YahooQuotes { get; }
    public HistoryBaseTests(ITestOutputHelper output) : base(output, LogLevel.Debug)
    {
        YahooQuotes = new YahooQuotesBuilder()
            .WithLogger(Logger)
            .WithHistoryStartDate(Instant.FromUtc(2024, 8, 1, 0, 0))
            .DoNotUseAdjustedClose()
            .Build();
    }

    [Theory]
    [InlineData("USD=X", "USD=X", 1)]
    [InlineData("EUR=X", "EUR=X", 1)]
    [InlineData("USD=X", "EUR=X", 0.9239)]
    [InlineData("EUR=X", "USD=X", 1.0824)]
    [InlineData("EUR=X", "JPY=X", 162.0817)]
    [InlineData("JPY=X", "USD=X", .0067)]
    public async Task CurrencyCurrencyTest(string currencySymbol, string baseCurrency, double firstBasePrice)
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync(currencySymbol, baseCurrency);
        History history = result.Value;
        BaseTick firstBaseTick = history.BaseTicks[0];

        if (currencySymbol == baseCurrency)
        {
            Assert.Equal(firstBasePrice, firstBaseTick.Price);
            return;
        }

        DateTimeZone tz = DateTimeZoneProviders.Tzdb[history.ExchangeTimezoneName];
        Write($"{currencySymbol} -> {baseCurrency}");
        Write($"Date: {firstBaseTick.Date}/{firstBaseTick.Date.InZone(tz)}, {firstBaseTick.Price} ({baseCurrency})");

        Assert.Equal(firstBasePrice, firstBaseTick.Price, 4);
    }

    [Theory]
    [InlineData("SPY", "USD=X", 543.01, 543.01)]
    [InlineData("SPY", "JPY=X", 543.01, 81054.5)]
    [InlineData("ISF.L", "GBP=X", 806, 806)]
    [InlineData("ISF.L", "USD=X", 806, 1029.06)]
    [InlineData("ISF.L", "JPY=X", 806, 153711.59)]
    public async Task StockCurrencyTest(string stockSymbol, string baseCurrency, double firstPrice, double firstBasePrice)
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync(stockSymbol, baseCurrency);
        History history = result.Value;
        string currency = history.Currency.Name;

        DateTimeZone tz = DateTimeZoneProviders.Tzdb[history.ExchangeTimezoneName];
        Tick firstTick = history.Ticks[0];
        BaseTick firstBaseTick = history.BaseTicks[0];
        Write($"{stockSymbol}/{currency} -> {baseCurrency}");
        Write($"Date: {firstTick.Date}/{firstTick.Date.InZone(tz)}, {firstTick.Close} ({currency})");
        Write($"Date: {firstBaseTick.Date}/{firstBaseTick.Date.InZone(tz)}, {firstBaseTick.Price} ({baseCurrency})");

        Assert.Equal(firstPrice, firstTick.Close, 2);
        Assert.Equal(firstBasePrice, firstBaseTick.Price, 2);
    }

    [Theory]
    [InlineData("USD=X", "SPY", 0.001842)]
    [InlineData("EUR=X", "SPY", 0.001987)]
    [InlineData("USD=X", "ISF.L", 0.000972)]
    [InlineData("GBP=X", "ISF.L", 0.001241)]
    [InlineData("EUR=X", "ISF.L", 0.001049)]
    public async Task CurrencyStockTest(string currencySymbol, string baseStockSymbol, double firstBasePrice = 0)
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync(currencySymbol, baseStockSymbol);
        History history = result.Value;
        Assert.False(history.Currency.IsValid); 

        DateTimeZone tz = DateTimeZoneProviders.Tzdb[history.ExchangeTimezoneName];
        BaseTick firstBaseTick = history.BaseTicks[0];
        Write($"{currencySymbol} -> {baseStockSymbol}");
        Write($"Date: {firstBaseTick.Date}/{firstBaseTick.Date.InZone(tz)}, {firstBaseTick.Price} ({baseStockSymbol})");

        Assert.Equal(firstBasePrice, firstBaseTick.Price, 6);
    }

    [Theory]
    [InlineData("SPY", "SPY", 1)]
    [InlineData("ISF.L", "ISF.L", 1)]
    [InlineData("SPY", "QQQ", 1.18)]
    [InlineData("ISF.L", "SPY", 1.90)]
    [InlineData("ISF.L", "2800.HK", 453.10)]
    public async Task StockStockTest(string stockSymbol, string baseStockSymbol, double firstPrice)
    {
        Result<History> result = await YahooQuotes.GetHistoryAsync(stockSymbol, baseStockSymbol);
        History history = result.Value;
        string currency = history.Currency.Name;
        DateTimeZone tz = DateTimeZoneProviders.Tzdb[history.ExchangeTimezoneName];
        BaseTick firstBaseTick = history.BaseTicks[0];

        if (stockSymbol == baseStockSymbol)
        {
            Assert.Equal(firstPrice, firstBaseTick.Price);
            return;
        }

        Write($"{stockSymbol}/{currency} -> {baseStockSymbol}");
        Write($"Date: {firstBaseTick.Date}/{firstBaseTick.Date.InZone(tz)}, {firstBaseTick.Price} ({baseStockSymbol})");

        Assert.Equal(firstPrice, firstBaseTick.Price, 2);
    }
}


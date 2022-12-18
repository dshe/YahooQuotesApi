using NodaTime;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests;

public class HistoryBaseTests : TestBase
{
    private readonly YahooQuotes MyYahooQuotes;
    public HistoryBaseTests(ITestOutputHelper output) : base(output)
    {
        MyYahooQuotes = new YahooQuotesBuilder()
            .WithLogger(Logger)
            .WithHistoryStartDate(Instant.FromUtc(2022, 1, 1, 0, 0))
            .WithNonAdjustedClose() // for testing
            .Build();
    }

    [Theory]
    [InlineData("C", "", 0)]
    [InlineData("C", "USD=X", 0)]
    [InlineData("C", "JPY=X", 0)]
    [InlineData("C", "SPY", 0)]
    [InlineData("C", "C", 0)]
    [InlineData("C", "JPYCHF=X", 1)]
    [InlineData("CHF=X", "", 1)]
    [InlineData("CHF=X", "JPY=X", 0)]
    [InlineData("CHF=X", "CHF=X", 0)]
    [InlineData("CAD=X", "USD=X", 0)]
    [InlineData("USD=X", "USD=X", 2)]
    [InlineData("CHF=X", "X", 0)]
    [InlineData("CHF=X", "JPYCHF=X", 1)]
    [InlineData("JPYCHF=X", "", 0)]
    [InlineData("JPYCHF=X", "JPY=X", 1)]
    [InlineData("JPYCHF=X", "JPYCHF=X", 1)]
    [InlineData("JPYCHF=X", "X", 1)]
    [InlineData("YNDX", "USD=X", 0)] // halted security
    [InlineData("THB=X", "USD=X", 0)] // odd? currency
    [InlineData("USDTHB=X", "", 0)] // odd? currency

    public async Task Test01Arguments(string symbol, string baseSymbol, int error)
    {
        var task = MyYahooQuotes.GetAsync(symbol, Histories.PriceHistory, baseSymbol);
        if (error == 1)
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await task);
            return;
        }

        Security security = await task ?? throw new InvalidOperationException();

        if (error == 2)
        {
            Assert.True(security.PriceHistoryBase.HasError);
            return;
        }
        Assert.True(security.PriceHistoryBase.HasValue);
    }

    [Theory]
    [InlineData("C", "USD=X", 63.10)]
    //[InlineData("C", "JPY=X")]
    //[InlineData("C", "SPY")]
    //[InlineData("C", "1306.T")]
    //[InlineData("1306.T", "JPY=X")]
    //[InlineData("1306.T", "USD=X")]
    //[InlineData("1306.T", "SPY")]
    //[InlineData("CHF=X", "JPY=X")]
    //[InlineData("CHF=X", "CHF=X")]
    //[InlineData("CHF=X", "X")]
    public async Task Test02Arguments(string symbol, string baseSymbol, double expected)
    {
        Security security = await MyYahooQuotes.GetAsync(symbol, Histories.PriceHistory, baseSymbol)
            ?? throw new ArgumentException("Invalid symbol");
        //var currency = security.Currency.ToUpper() + "=X";
        Assert.True(security.PriceHistory.HasValue);
        PriceTick priceTick = security.PriceHistory.Value[0];

        if (baseSymbol == "USD=X")
        {
            Assert.Equal(expected, priceTick.Close, 2);
            return;
        }
        Assert.True(security.PriceHistoryBase.HasValue);
        ValueTick valueTick = security.PriceHistoryBase.Value[0];

        Security baseSecurity = await MyYahooQuotes.GetAsync(baseSymbol, Histories.PriceHistory)
            ?? throw new ArgumentException("Invalid symbol");
        Assert.True(baseSecurity.PriceHistory.HasValue);

        //PriceTick baseTick = baseSecurity.PriceHistory.Value[0];
        //Write($"{symbol} + {baseSymbol} => {tick}");

        Assert.Equal(expected, valueTick.Value, 2);
    }

    [Fact]
    public async Task Test02NoHistory()
    {
        // Symbol SFO does not have history.
        var result = await MyYahooQuotes.GetAsync("SFO", Histories.PriceHistory, "USD=X");
        Assert.True(result?.PriceHistory.HasError);
        Assert.True(result?.PriceHistoryBase.HasError);
    }

    [Theory]
    [InlineData("SPY", "USD=X")]
    [InlineData("SPY", "JPY=X")]
    [InlineData("7203.T", "JPY=X")]
    [InlineData("7203.T", "USD=X")]
    [InlineData("7203.T", "EUR=X")]
    public async Task Test03SecurityBaseCurrency(string symbol, string baseSymbol)
    {
        var security = await MyYahooQuotes.GetAsync(symbol, Histories.PriceHistory, baseSymbol) ?? throw new Exception($"Unknown symbol: {symbol}.");
        var currency = security.Currency + "=X";
        var date = security.PriceHistoryBase.Value.First().Date;
        var price = security.PriceHistory.Value.First().Close;

        if (currency != baseSymbol)
        {
            if (baseSymbol != "USD=X")
            {
                var rateSymbol = "USD" + baseSymbol;
                var sec = await MyYahooQuotes.GetAsync(rateSymbol, Histories.PriceHistory) ?? throw new Exception($"Unknown symbol:?");
                var rate = sec.PriceHistoryBase.Value.InterpolateValue(date);
                price *= rate;
            }
            if (currency != "USD=X")
            {
                var rateSymbol = "USD" + currency;
                var sec = await MyYahooQuotes.GetAsync(rateSymbol, Histories.PriceHistory) ?? throw new Exception($"Unknown symbol: {rateSymbol}.");
                var rate = sec.PriceHistoryBase.Value.InterpolateValue(date);
                price /= rate;
            }
        }
        Assert.Equal(security.PriceHistoryBase.Value.First().Value, price, 6);
    }

    [Theory]
    //[InlineData("SPY", "SPY")]
    [InlineData("GOOG", "SPY")]
    //[InlineData("7203.T", "7203.T")]
    //[InlineData("7203.T", "1306.T")]
    [InlineData("SPY", "7203.T")]
    //[InlineData("7203.T", "HXT.TO")]
    public async Task Test04SecurityBaseSecurity(string symbol, string baseSymbol)
    {
        var security = await MyYahooQuotes.GetAsync(symbol, Histories.PriceHistory, baseSymbol) ?? throw new Exception($"Unknown symbol: {symbol}.");
        var currency = security.Currency + "=X";
        var date = security.PriceHistoryBase.Value.First().Date;
        var price = security.PriceHistory.Value.First().Close;
        var baseSecurity = await MyYahooQuotes.GetAsync(baseSymbol, Histories.PriceHistory) ?? throw new Exception($"Unknown symbol: {baseSymbol}.");
        var baseSecurityCurrency = baseSecurity.Currency + "=X";

        if (currency != baseSecurityCurrency)
        {
            if (baseSecurityCurrency != "USD=X")
            {
                var rateSymbol = "USD" + baseSecurityCurrency;
                var secCurrency = await MyYahooQuotes.GetAsync(rateSymbol, Histories.PriceHistory) ?? throw new Exception($"Unknown symbol: {baseSymbol}.");
                var rate = secCurrency.PriceHistoryBase.Value.InterpolateValue(date);
                price *= rate;
            }
            if (currency != "USD=X")
            {
                var rateSymbol = "USD" + currency;
                var secCurrency = await MyYahooQuotes.GetAsync(rateSymbol, Histories.PriceHistory) ?? throw new Exception($"Unknown symbol: {rateSymbol}.");
                var rate = secCurrency.PriceHistoryBase.Value.InterpolateValue(date);
                price /= rate;
            }
        }

        var rate3 = baseSecurity.PriceHistoryBase.Value.InterpolateValue(date);
        price /= rate3;

        Assert.Equal(security.PriceHistoryBase.Value.First().Value, price, 6);
    }

    [Theory] // (AAA=X BBB=X) => AAABBB=X
             //[InlineData("JPY=X", "USD=X")] // same base => no change
             //[InlineData("USD=X", "JPY=X")] // change base to JPY
    [InlineData("EUR=X", "CAD=X")] // change base to CAD
    public async Task Test05CurrencyBaseCurrency(string currencySymbol, string baseCurrencySymbol)
    {
        Security security = await MyYahooQuotes.GetAsync(currencySymbol, Histories.PriceHistory, baseCurrencySymbol) ?? throw new Exception($"Unknown symbol: {currencySymbol}.");

        var date = security.PriceHistoryBase.Value.First().Date;
        var resultFound = security.PriceHistoryBase.Value.First().Value;

        var symbol = $"{currencySymbol[..3]}{baseCurrencySymbol}";
        var security2 = await MyYahooQuotes.GetAsync(symbol, Histories.PriceHistory) ?? throw new Exception($"Unknown symbol: {symbol}.");
        var priceHistory = security2.PriceHistoryBase.Value;
        var rate = priceHistory.InterpolateValue(date);
        Assert.Equal(rate, resultFound, 2);
    }

    [Theory]
    [InlineData("USD=X", "SPY")]
    [InlineData("JPY=X", "SPY")]
    [InlineData("JPY=X", "7203.T")]
    [InlineData("CHF=X", "7203.T")]
    public async Task Test06CurrencyBaseSecurity(string symbol, string baseSymbol)
    {
        var security = await MyYahooQuotes.GetAsync(symbol, Histories.PriceHistory, baseSymbol) ?? throw new Exception($"Unknown symbol: {symbol}.");
        var date = security.PriceHistoryBase.Value[0].Date;
        var price = 1d;

        if (symbol != "USD=X")
        {
            var rateSymbol = "USD" + symbol;
            var sec1 = await MyYahooQuotes.GetAsync(rateSymbol, Histories.PriceHistory) ?? throw new Exception($"Unknown symbol:?");
            var rate = sec1.PriceHistoryBase.Value.InterpolateValue(date);
            price /= rate;
        }

        var sec2 = await MyYahooQuotes.GetAsync(baseSymbol, Histories.PriceHistory) ?? throw new Exception($"Unknown symbol: {baseSymbol}.");
        var value = sec2.PriceHistoryBase.Value.InterpolateValue(date);
        price /= value;

        if (sec2.Currency != "USD")
        {
            var rateSymbol = $"USD{sec2.Currency}=X";
            var sec3 = await MyYahooQuotes.GetAsync(rateSymbol, Histories.PriceHistory) ?? throw new Exception($"Unknown symbol: {rateSymbol}.");
            var rate = sec3.PriceHistoryBase.Value.InterpolateValue(date);
            price *= rate;
        }

        Assert.Equal(security.PriceHistoryBase.Value[0].Value, price);
    }
}

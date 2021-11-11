using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class HistoryBaseTests : TestBase
    {
        private readonly YahooQuotes MyYahooQuotes;
        public HistoryBaseTests(ITestOutputHelper output) : base(output, LogLevel.Debug)
        {
            MyYahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(Instant.FromUtc(2020, 1, 1, 0, 0))
                .UseNonAdjustedClose() // for testing
                .Build();
        }

        [Theory]
        [InlineData("C", null, 0)]
        [InlineData("C", "JPY=X", 0)]
        [InlineData("C", "X", 0)]
        /*
        [InlineData("C", "JPYCHF=X", 1)]
        [InlineData("CHF=X", "", 1)]
        [InlineData("CHF=X", "JPY=X", 0)]
        [InlineData("CHF=X", "CHF=X", 0)]
        [InlineData("USD=X", "USD=X", 2)]
        [InlineData("CHF=X", "X", 0)]
        [InlineData("CHF=X", "JPYCHF=X", 1)]
        [InlineData("JPYCHF=X", "", 0)]
        [InlineData("JPYCHF=X", "JPY=X", 1)]
        [InlineData("JPYCHF=X", "JPYCHF=X", 1)]
        [InlineData("JPYCHF=X", "X", 1)]
        */
        public async Task Test01Arguments(string symbol, string baseSymbol, int error)
        {
            var task = MyYahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory, baseSymbol);
            if (error == 0)
            {
                var result = await task;
                Assert.True(result?.PriceHistoryBase.HasValue);
            }
            if (error == 1)
            {
                await Assert.ThrowsAsync<ArgumentException>(async () => await task);
            }
            if (error == 2)
            {
                var result = await task;
                Assert.True(result?.PriceHistoryBase.HasError);
            }

        }

        [Fact]
        public async Task Test02NoHistory()
        {
            // Symbol SFO does not have history.
            var result = await MyYahooQuotes.GetAsync("SFO", HistoryFlags.PriceHistory, "USD=X");
            Assert.True(result?.PriceHistory.HasError);
            Assert.True(result?.PriceHistoryBase.HasError);
        }

        [Theory]
        [InlineData("SPY", "USD=X")]
        //[InlineData("SPY", "JPY=X")]
        //[InlineData("7203.T", "JPY=X")]
        //[InlineData("7203.T", "USD=X")]
        [InlineData("7203.T", "EUR=X")]
        public async Task Test03SecurityBaseCurrency(string symbol, string baseCurrency)
        {
            var security = await MyYahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory, baseCurrency) ?? throw new Exception($"Unknown symbol: {symbol}.");
            var currency = security.Currency + "=X";
            var date = security.PriceHistoryBase.Value.First().Date;
            var price = security.PriceHistory.Value.First().Close;

            if (currency != baseCurrency)
            {
                if (baseCurrency != "USD=X")
                {
                    var rateSymbol = "USD" + baseCurrency;
                    var sec = await MyYahooQuotes.GetAsync(rateSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol:?");
                    var rate = sec.PriceHistoryBase.Value.InterpolateValue(date);
                    price *= rate;
                }
                if (currency != "USD=X")
                {
                    var rateSymbol = "USD" + currency;
                    var sec = await MyYahooQuotes.GetAsync(rateSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {rateSymbol}.");
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
            var security = await MyYahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory, baseSymbol) ?? throw new Exception($"Unknown symbol: {symbol}.");
            var currency = security.Currency + "=X";
            var date = security.PriceHistoryBase.Value.First().Date;
            var price = security.PriceHistory.Value.First().Close;
            var baseSecurity = await MyYahooQuotes.GetAsync(baseSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {baseSymbol}.");
            var baseSecurityCurrency = baseSecurity.Currency + "=X";

            if (currency != baseSecurityCurrency)
            {
                if (baseSecurityCurrency != "USD=X")
                {
                    var rateSymbol = "USD" + baseSecurityCurrency;
                    var secCurrency = await MyYahooQuotes.GetAsync(rateSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {baseSymbol}.");
                    var rate = secCurrency.PriceHistoryBase.Value.InterpolateValue(date);
                    price *= rate;
                }
                if (currency != "USD=X")
                {
                    var rateSymbol = "USD" + currency;
                    var secCurrency = await MyYahooQuotes.GetAsync(rateSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {rateSymbol}.");
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
            Security security = await MyYahooQuotes.GetAsync(currencySymbol, HistoryFlags.PriceHistory, baseCurrencySymbol) ?? throw new Exception($"Unknown symbol: {currencySymbol}.");

            var date = security.PriceHistoryBase.Value.First().Date;
            var resultFound = security.PriceHistoryBase.Value.First().Value;

            var symbol = $"{currencySymbol[..3]}{baseCurrencySymbol}";
            var security2 = await MyYahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {symbol}.");
            var priceHistory = security2.PriceHistoryBase.Value;
            var rate = priceHistory.InterpolateValue(date);
            Assert.Equal(rate, resultFound, 2);
        }

        [Theory]
        //[InlineData("USD=X", "SPY")]
        [InlineData("JPY=X", "SPY")]
        //[InlineData("JPY=X", "7203.T")]
        //[InlineData("CHF=X", "7203.T")]
        public async Task Test06CurrencyBaseSecurity(string symbol, string baseSymbol)
        {
            var security = await MyYahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory, baseSymbol) ?? throw new Exception($"Unknown symbol: {symbol}.");
            var date = security.PriceHistoryBase.Value[0].Date;
            var price = 1d;

            if (symbol != "USD=X")
            {
                var rateSymbol = "USD" + symbol;
                var sec1 = await MyYahooQuotes.GetAsync(rateSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol:?");
                var rate = sec1.PriceHistoryBase.Value.InterpolateValue(date);
                price /= rate;
            }

            var sec2 = await MyYahooQuotes.GetAsync(baseSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {baseSymbol}.");
            var value = sec2.PriceHistoryBase.Value.InterpolateValue(date);
            price /= value;

            if (sec2.Currency != "USD")
            {
                var rateSymbol = $"USD{sec2.Currency}=X";
                var sec3 = await MyYahooQuotes.GetAsync(rateSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {rateSymbol}.");
                var rate = sec3.PriceHistoryBase.Value.InterpolateValue(date);
                price *= rate;
            }

            Assert.Equal(security.PriceHistoryBase.Value[0].Value, price);
        }
    }
}

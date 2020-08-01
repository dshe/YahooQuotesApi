using NodaTime;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class TestHistoryBase : TestBase
    {
        public TestHistoryBase(ITestOutputHelper output) : base(output) { }

        [Theory]
        [InlineData("SPY", "USD=X")]
        [InlineData("SPY", "JPY=X")]
        [InlineData("7203.T", "JPY=X")]
        [InlineData("7203.T", "USD=X")]
        [InlineData("7203.T", "EUR=X")]
        public async Task TestSecurityBaseCurrency(string symbol, string baseSymbol)
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(Instant.FromUtc(2020, 7, 1, 0, 0))
                .Build();

            Security security1 = await yahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory, baseSymbol) ?? throw new Exception($"Unknown symbol: {symbol}.");
            var date = security1.PriceHistoryBase.First().Date.ToInstant();
            var resultFound = security1.PriceHistoryBase.First().Close;

            /* ------------------------------------ */

            Security security2 = await yahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {symbol}.");
            var priceHistory = security2.PriceHistory ?? throw new Exception($"No price history: {symbol}.");
            var price = priceHistory.Interpolate(date);
            var result = price;

            if (security2.Currency != baseSymbol.Substring(0, 3))
            {
                if (security2.Currency != "USD")
                {
                    var rateSymbol = "USD" + security2.Currency + "=X";
                    var sec = await yahooQuotes.GetAsync(rateSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {rateSymbol}.");
                    var rate = sec.PriceHistory!.Interpolate(date);
                    price /= rate;
                }

                if (baseSymbol != "USD=X")
                {
                    var sec = await yahooQuotes.GetAsync("USD" + baseSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol:?");
                    var rate = sec.PriceHistory!.Interpolate(date);
                    price *= rate;
                }
            }

            Write($"{symbol} {baseSymbol} => {price} == {resultFound}.");
            Assert.Equal(price, resultFound);
        }

        [Theory]
        [InlineData("SPY", "SPY")]
        [InlineData("GOOG", "SPY")]
        [InlineData("7203.T", "7203.T")]
        [InlineData("7203.T", "1306.T")]
        [InlineData("7203.T", "SPY")]
        [InlineData("7203.T", "HXT.TO")]
        public async Task TestSecurityBaseSecurity(string symbol, string baseSymbol)
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(Instant.FromUtc(2020, 7, 1, 0, 0))
                .Build();

            Security security1 = await yahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory, baseSymbol) ?? throw new Exception($"Unknown symbol: {symbol}.");
            var date = security1.PriceHistoryBase.First().Date.ToInstant();
            var resultFound = security1.PriceHistoryBase.First().Close;

            /* ------------------------------------ */

            Security security2 = await yahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {symbol}.");
            var priceHistory = security2.PriceHistory ?? throw new Exception($"No price history: {symbol}.");
            var price = priceHistory.Interpolate(date);

            var sec = await yahooQuotes.GetAsync(baseSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {baseSymbol}.");
            if (security2.Currency != sec.Currency)
            {
                if (security2.Currency != "USD")
                {
                    var rateSymbol = $"USD{security2.Currency}=X";
                    var secx = await yahooQuotes.GetAsync(rateSymbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {rateSymbol}.");
                    var rate = secx.PriceHistory!.Interpolate(date);
                    price /= rate;
                }
                if (sec.Currency != "USD")
                {
                    var currency = $"USD{sec.Currency}=X";
                    var secCurrency = await yahooQuotes.GetAsync(currency, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {baseSymbol}.");
                    var rate = secCurrency.PriceHistory!.Interpolate(date);
                    price *= rate;
                }
            }

            var rate3 = sec.PriceHistory!.Interpolate(date);
            price /= rate3;

            Write($"{symbol} {baseSymbol} => {price} == {resultFound}.");
            Assert.Equal(price, resultFound);
        }

        [Theory] // (AAA=X BBB=X) => AAABBB=X
        [InlineData("JPY=X", "USD=X")] // same base => no change
        [InlineData("USD=X", "JPY=X")] // change base to JPY
        [InlineData("EUR=X", "CAD=X")] // change base to MYR
        public async Task TestCurrencyBaseCurrency(string currencyRateSymbol, string baseCurrencySymbol)
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(Instant.FromUtc(2020, 7, 1, 0, 0))
                .Build();

            Security security = await yahooQuotes.GetAsync(currencyRateSymbol, HistoryFlags.PriceHistory, baseCurrencySymbol) ?? throw new Exception($"Unknown symbol: {currencyRateSymbol}.");
            var date = security.PriceHistoryBase.First().Date.ToInstant();
            var resultFound = security.PriceHistoryBase.First().Close;

            /* ------------------------------------ */

            var symbol = $"{currencyRateSymbol.Substring(0, 3)}{baseCurrencySymbol}";
            var security2 = await yahooQuotes.GetAsync(symbol, HistoryFlags.PriceHistory) ?? throw new Exception($"Unknown symbol: {symbol}.");
            var priceHistory = security2.PriceHistory ?? throw new Exception($"No price history: {symbol}.");
            var rate = priceHistory.Interpolate(date);
            Assert.Equal(rate, resultFound, 1);
        }

        [Fact]
        public async Task TestNoCurrency()
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(Instant.FromUtc(2020, 7, 1, 0, 0))
                .Build();

            // Symbol SFO does not specify a currency.
            await Assert.ThrowsAsync<ArgumentException>(async() =>
                await yahooQuotes.GetAsync("SFO", HistoryFlags.PriceHistory, "USD=X"));
        }

        [Fact]
        public async Task TestUSDUSD()
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(Instant.FromUtc(2020, 7, 1, 0, 0))
                .Build();

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await yahooQuotes.GetAsync("USD=X", HistoryFlags.PriceHistory, "USD=X"));
        }

        //[Fact]
        public async Task TestXIU()
        {
            YahooQuotes yahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(Instant.FromUtc(2019, 1, 1, 0, 0))
                .Build();

            var result = await yahooQuotes.GetAsync("XIU.TO", HistoryFlags.PriceHistory, "USD=X");
        }

    }
}

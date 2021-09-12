using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class ArgumentTests : TestBase
    {
        private readonly YahooQuotes YahooQuotes;

        public ArgumentTests(ITestOutputHelper output) : base(output, LogLevel.Trace) =>
            YahooQuotes = new YahooQuotesBuilder(Logger).Build();

        [Fact]
        public async Task NullAndEmptySymbolTest()
        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await YahooQuotes.GetAsync((string)null));
            _ = await Assert.ThrowsAsync<ArgumentException>(async () => await YahooQuotes.GetAsync(""));
            _ = await YahooQuotes.GetAsync(Array.Empty<string>());
            _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await YahooQuotes.GetAsync((string[])null));
            _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await YahooQuotes.GetAsync(new string[] { null }));
            _ = await Assert.ThrowsAsync<ArgumentException>(async () => await YahooQuotes.GetAsync(new string[] { "" }));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
        }

        [Theory]
        [InlineData("C ")]
        [InlineData("=X")]
        [InlineData("JPY=X")]
        [InlineData("JPYX=X")]
        public async Task TestInvalidSymbols(string symbol)
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await YahooQuotes.GetAsync(symbol));
        }

        [Fact]
        public async Task UnknownSymbolTest()
        {
            Assert.Null(await YahooQuotes.GetAsync("UnknownSymbol"));
        }

        [Fact]
        public async Task OkSymbolsTest()
        {
            var securities = await YahooQuotes.GetAsync(new string[] { "AAPL", "MSFT", "USDJPY=X" });
            Assert.Equal(3, securities.Count);
        }

        [Fact]
        public async Task TestFieldAccess()
        {
            Security security = await YahooQuotes.GetAsync("AAPL") ?? throw new ArgumentException();
            Assert.Equal("Apple Inc.", security.LongName);   // static type
        }

        [Fact]
        public async Task IgnoreDuplicateTest()
        {
            var symbols = new[] { "C", "X", "MSFT", "C" }; ;
            var ysecurities = await YahooQuotes.GetAsync(symbols) ?? throw new ArgumentException();
            Assert.Equal(3, ysecurities.Count);
        }

        [Fact]
        public async Task CancellationTest()
        {
            var ct = new CancellationToken(true);
            var task1 = YahooQuotes.GetAsync("IBM", ct: ct);
            var e1 = await Assert.ThrowsAnyAsync<Exception>(async () => await task1);
            Assert.True(e1 is OperationCanceledException);
        }
    }
}

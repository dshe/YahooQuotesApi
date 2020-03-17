using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class ExchangesTest
    {
        private readonly Action<string> Write;
        public ExchangesTest(ITestOutputHelper output) => Write = output.WriteLine;

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(".")]
        [InlineData(".X")]
        [InlineData("X.")]
        public void TestGetSuffixThrow(string symbol)
        {
            Assert.ThrowsAny<Exception>(() => Exchanges.GetSuffix(symbol));
        }

        [Theory]
        [InlineData("FBU", "")]
        [InlineData("FBU.NZ", "NZ")]
        public void TestGetSuffix(string symbol, string suffix)
        {
            Assert.Equal(suffix, Exchanges.GetSuffix(symbol));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class TestSymbolStruct : TestBase
    {
        public TestSymbolStruct(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Test()
        {
            Assert.True(new Symbol("abc") == new Symbol("abc"));
            Assert.Equal(new Symbol("abc"), new Symbol("abc"));
            Assert.NotEqual(new Symbol("abc"), new Symbol("def"));

            var list = new List<Symbol>
            {
                new Symbol("c5"),
                new Symbol("c2"),
                new Symbol("c1"),
                new Symbol("c4")
            };

            list.Sort();
            Assert.Equal(new Symbol("c1"), list.First());

            var dict = list.ToDictionary(x => x, x => x.Name);
            var result = dict[new Symbol("c2")];
            Assert.Equal("C2", result);

            var x = new Symbol();
            string y = x.Name;
            Assert.Null(y);


        }
    }
}

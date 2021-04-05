using NodaTime;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class AsyncItemsCacheTests : TestBase
    {
        public AsyncItemsCacheTests(ITestOutputHelper output) : base(output) { }

        private readonly AsyncItemsCache<string, string> Cache
            = new AsyncItemsCache<string, string>(Duration.FromDays(1));

        private int Produces = 0;

        private async Task<Dictionary<string,string>> Producer(List<string> keys)
        {
            Write($"producing using keys: {string.Join(',', keys)}");
            await Task.Yield();
            Produces++;
            var d = new Dictionary<string, string>();
            foreach (var key in keys)
                d.Add(key, $"value for key: {key} {Produces}.");
            return d;
        }

        private async Task<Dictionary<string, string>> Get(List<string> keys)
        {
            Write($"getting keys: {string.Join(',', keys)}");
            return await Cache.Get(keys, () => Producer(keys));
        }

        [Fact]
        public async Task TestCache1()
        {
            var result1 = await Get(new List<string>() { "a", "b", "c" });
            var result2 = await Get(new List<string>() { "b" });
            var result3 = await Get(new List<string>() { "c", "a" });
            var result4 = await Get(new List<string>() { "b", "z" });
            ;
            Assert.Equal(2, Produces);
        }
    }
}

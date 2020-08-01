using NodaTime;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class AsyncItemsCacheTest : TestBase
    {
        public AsyncItemsCacheTest(ITestOutputHelper output) : base(output) { }

        private readonly AsyncItemsCache<string, string> Cache
            = new AsyncItemsCache<string, string>(Duration.FromDays(1));

        private int Produces = 0;

        private async Task<Dictionary<string,string>> Producer(List<string> keys)
        {
            //Write($"producing using key {key}");
            await Task.Yield();
            Produces++;
            var d = new Dictionary<string, string>();
            foreach (var key in keys)
                d.Add(key, "value" + key + " " + Produces);
            return d;
        }

        private async Task<Dictionary<string, string>> Get(List<string> keys)
        {
            //Write($"getting key {key}");
            return await Cache.Get(keys, () => Producer(keys));
        }


        [Fact]
        public async Task TestCache1()
        {
            var result1 = await Get(new List<string>() { "1" });
            var result2 = await Get(new List<string>() { "1." });
            ;
            //Assert.Equal(3, Produces);
        }
    }
}

using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class AsyncQueueTests : TestBase
    {
        public AsyncQueueTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task Test2()
        {
            var AsyncQueue = new AsyncQueue<string>(100);

            var task = Task.Run(() => {
                AsyncQueue.Add(new[] { "one" });
                AsyncQueue.Add(new[] { "two", "three" });
                AsyncQueue.Add(new[] { "four" });
            });

            var xx =await AsyncQueue.RemoveAllAsync();
            Assert.Equal(4, xx.Count());

            await task;


        }
    }
}

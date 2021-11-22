using NodaTime;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace YahooQuotesApi.Tests;

public class AsyncItemCacheTest : TestBase
{
    public AsyncItemCacheTest(ITestOutputHelper output) : base(output) { }

    private readonly ParallelProducerCache<string, string> Cache =
        new ParallelProducerCache<string, string>(SystemClock.Instance, Duration.MaxValue);

    private int Produces = 0;

    private async Task<string> Producer(string key)
    {
        Write($"producing using key {key}");
        await Task.Yield();
        Produces++;
        return "result";
    }

    private async Task<string> Get(string key)
    {
        Write($"getting key {key}");
        return await Cache.Get(key, () => Producer(key));
    }

    [Fact]
    public async Task TestCache1()
    {
        await Get("1");
        await Get("2");
        await Get("2");
        await Get("2");
        await Get("3");
        await Get("3");
        await Get("1");
        Assert.Equal(3, Produces);
    }
}

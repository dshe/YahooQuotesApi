using NodaTime;
namespace YahooQuotesApi.SnapshotTest;

public class SerialProducerCacheTest : XunitTestBase
{
    private readonly SerialProducerCache<int, string> Cache;
    private readonly List<string> RequestHistory = [];
    public SerialProducerCacheTest(ITestOutputHelper output) : base(output)
    {
        Cache = new SerialProducerCache<int, string>(SystemClock.Instance, Duration.MaxValue, Producer);
    }

    private async Task<Dictionary<int, string>> Producer(IEnumerable<int> keys, CancellationToken ct)
    {
        string msg = string.Join(", ", keys);
        RequestHistory.Add(msg);
        Dictionary<int, string> results = new();
        foreach (var key in keys)
        {
            results.Add(key, msg);
        }
        await Task.CompletedTask;
        return results;
    }

    [Fact]
    public async Task Test()
    {
        await Cache.GetAsync([1, 2, 3], TestContext.Current.CancellationToken);
        Dictionary<int, string> result = await Cache.GetAsync([1, 2], TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Count);
        Assert.Single(RequestHistory);
        result = await Cache.GetAsync([6, 1], TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, RequestHistory.Count);
    }
}

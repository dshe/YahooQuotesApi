using NodaTime;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace YahooQuotesApi.Tests;

public class AsyncItemsCacheTests : TestBase
{
    private readonly SerialProducerCache<int, string> Cache;
    private readonly List<string> RequestHistory = new List<string>();
    public AsyncItemsCacheTests(ITestOutputHelper output) : base(output)
    {
        Cache = new SerialProducerCache<int, string>(SystemClock.Instance, Duration.MaxValue, Producer);
    }

    private async Task<Dictionary<int, string>> Producer(IEnumerable<int> keys, CancellationToken ct)
    {
        var msg = String.Join(", ", keys);
        RequestHistory.Add(msg);
        var results = new Dictionary<int, string>();
        foreach (var key in keys)
        {
            results.Add(key, msg);
        }
        await Task.CompletedTask;
        return results;
    }

    [Fact]
    public async Task Test2()
    {
        await Cache.Get(new HashSet<int> { 1, 2, 3 }, default);
        var result = await Cache.Get(new HashSet<int> { 1, 2 }, default);
        Assert.Equal(2, result.Count);
        Assert.Single(RequestHistory);
        result = await Cache.Get(new HashSet<int> { 6, 1 }, default);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, RequestHistory.Count);
        ;
    }
}

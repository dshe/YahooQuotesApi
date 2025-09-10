using NodaTime;
namespace YahooQuotesApi.UtilityTests;

public class IncreasingTest(ITestOutputHelper output) : XunitTestBase(output, LogLevel.Trace)
{
    [Fact]
    public void Test1()
    {
        int[] list = [1, 2, 3, 17, 18, 19, 20];
        Assert.True(list.IsIncreaing((x1,x2) => x1 < x2));
    }

    [Fact]
    public void Test2()
    {
        Instant[] list = [Instant.MinValue, Instant.FromUnixTimeSeconds(10), Instant.MaxValue];
        Assert.Null(list.IsIncreasing(x => x));
    }

}


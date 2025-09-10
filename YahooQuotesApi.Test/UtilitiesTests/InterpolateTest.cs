using NodaTime;
namespace YahooQuotesApi.UtilityTests;

public class InterpolateTest(ITestOutputHelper output) : XunitTestBase(output)
{
    [Fact]
    public void TestNotEnoughData()
    {
        var ticks = new List<BaseTick>();
        Assert.Throws<ArgumentException>(() => ticks.ToArray().InterpolatePrice(new Instant()));

        ticks.Add(new BaseTick(new Instant(), 1, 0));
        Assert.Throws<ArgumentException>(() => ticks.ToArray().InterpolatePrice(new Instant()));

        ticks.Add(new BaseTick(new Instant(), 1, 0));
        Assert.Equal(1, ticks.ToArray().InterpolatePrice(new Instant()));

        ticks.Add(new BaseTick(new Instant(), 1, 0));
        Assert.Equal(1, ticks.ToArray().InterpolatePrice(new Instant()));
    }

    [Fact]
    public void BoundaryTest()
    {
        var ticks = new List<BaseTick>();
        var date1 = new LocalDateTime(2000, 1, 1, 0, 0).InUtc().ToInstant();
        var date2 = new LocalDateTime(2000, 1, 2, 0, 0).InUtc().ToInstant();

        ticks.Add(new BaseTick(date1, 1, 0));
        ticks.Add(new BaseTick(date2, 1, 0));

        var result = ticks.ToArray().InterpolatePrice(date1);
        Assert.False(double.IsNaN(result)); // enough data

        result = ticks.ToArray().InterpolatePrice(date1.Plus(Duration.FromHours(-7 * 24)));
        Assert.True(double.IsNaN(result)); // not enough data

        result = ticks.ToArray().InterpolatePrice(date2);
        Assert.False(double.IsNaN(result)); // enough data

        result = ticks.ToArray().InterpolatePrice(date2.Plus(Duration.FromHours(7 * 24)));
        Assert.True(double.IsNaN(result)); // not enough data
    }

    [Fact]
    public void BoundaryLimitTest()
    {
        var ticks = new List<BaseTick>();
        var date1 = new LocalDateTime(2000, 1, 1, 0, 0).InUtc().ToInstant();
        var date2 = new LocalDateTime(2000, 1, 2, 0, 0).InUtc().ToInstant();

        ticks.Add(new BaseTick(date1, 1, 0));
        ticks.Add(new BaseTick(date2, 2, 0));

        var result = ticks.ToArray().InterpolatePrice(date2.PlusTicks(1));
        Assert.Equal(2, result);

        result = ticks.ToArray().InterpolatePrice(date2.Plus(Duration.FromHours(7 * 24)));
        Assert.True(double.IsNaN(result)); // not enough data
    }

    [Fact]
    public void InterpolateTest1()
    {
        var ticks = new List<BaseTick>();

        var date1 = new LocalDateTime(2000, 1, 1, 0, 0).InUtc().ToInstant();
        var date2 = new LocalDateTime(2000, 1, 5, 0, 0).InUtc().ToInstant();

        ticks.Add(new BaseTick(date1, 1, 0));
        ticks.Add(new BaseTick(date2, 2, 0));

        var result = ticks.ToArray().InterpolatePrice(date1.Plus(Duration.FromDays(1)));
        Assert.Equal(1.25, result);
    }

    [Fact]
    public void BinarySearch2Test2()
    {
        var list = new[] { 1.0 };

        IReadOnlyList<double> ilist = list;

        var index = ilist.ToArray().BinarySearch(0.0, x => x);
        Assert.Equal(~0, index);

        index = ilist.ToArray().BinarySearch(2.0, x => x);
        Assert.Equal(~1, index);
    }

    [Theory]
    [InlineData(-9.0, ~0)]
    [InlineData(1.0, 0)]
    [InlineData(2.0, 1)]
    [InlineData(2.5, ~2)] // if not found, bitwise complement of index of next larger element
    [InlineData(3.0, 2)]
    [InlineData(4.0, 3)]
    [InlineData(9.0, ~4)]
    public void BinarySearch2Test(double searchValue, int expectedIndex)
    {
        var list = new[] { 1.0, 2.0, 3.0, 4.0 };

        IReadOnlyList<double> ilist = list;

        var index = ilist.ToArray().BinarySearch(searchValue, x => x);
        Assert.Equal(expectedIndex, index);
    }
}

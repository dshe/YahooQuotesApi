using NodaTime;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
namespace YahooQuotesApi.Tests;

public class InterpolateTest : TestBase
{
    public InterpolateTest(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void TestNotEnoughData()
    {
        var list = new List<ValueTick>();
        Assert.Throws<ArgumentException>(() => list.InterpolateValue(new Instant()));
        list.Add(new ValueTick(new Instant(), 0, 0));

        Assert.Throws<ArgumentException>(() => list.InterpolateValue(new Instant()));
        list.Add(new ValueTick(new Instant(), 0, 0));
        Assert.Equal(0, list.InterpolateValue(new Instant()));
        list.Add(new ValueTick(new Instant(), 0, 0));
        Assert.Equal(0, list.InterpolateValue(new Instant()));
    }

    [Fact]
    public void BoundaryTest()
    {
        var list = new List<ValueTick>();
        var date1 = new LocalDateTime(2000, 1, 1, 0, 0).InUtc().ToInstant();
        var date2 = new LocalDateTime(2000, 1, 2, 0, 0).InUtc().ToInstant();

        list.Add(new ValueTick(date1, 0, 0));
        list.Add(new ValueTick(date2, 0, 0));

        var result = list.InterpolateValue(date1);
        Assert.False(double.IsNaN(result)); // enough data

        result = list.InterpolateValue(date1.Plus(Duration.FromHours(-7 * 24)));
        Assert.True(double.IsNaN(result)); // not enough data

        result = list.InterpolateValue(date2);
        Assert.False(double.IsNaN(result)); // enough data

        result = list.InterpolateValue(date2.Plus(Duration.FromHours(7 * 24)));
        Assert.True(double.IsNaN(result)); // not enough data
    }

    [Fact]
    public void BoundaryLimitTest()
    {
        var list = new List<ValueTick>();
        var date1 = new LocalDateTime(2000, 1, 1, 0, 0).InUtc().ToInstant();
        var date2 = new LocalDateTime(2000, 1, 2, 0, 0).InUtc().ToInstant();

        list.Add(new ValueTick(date1, 1, 0));
        list.Add(new ValueTick(date2, 2, 0));

        var result = list.InterpolateValue(date2.PlusTicks(1));
        Assert.Equal(2, result);

        result = list.InterpolateValue(date2.Plus(Duration.FromHours(7 * 24)));
        Assert.True(double.IsNaN(result)); // not enough data
    }

    [Fact]
    public void InterpolateTest1()
    {
        var list = new List<ValueTick>();

        var date1 = new LocalDateTime(2000, 1, 1, 0, 0).InUtc().ToInstant();
        var date2 = new LocalDateTime(2000, 1, 5, 0, 0).InUtc().ToInstant();

        list.Add(new ValueTick(date1, 1, 0));
        list.Add(new ValueTick(date2, 2, 0));

        var result = list.InterpolateValue(date1.Plus(Duration.FromDays(1)));
        Assert.Equal(1.25, result);
    }

    [Fact]
    public void BinarySearch2Test2()
    {
        var list = new[] { 1.0 };

        IReadOnlyList<double> ilist = list;

        var index = ilist.BinarySearch(0.0, x => x);
        Assert.Equal(~0, index);

        index = ilist.BinarySearch(2.0, x => x);
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

        var index = ilist.BinarySearch(searchValue, x => x);
        Assert.Equal(expectedIndex, index);
    }
}

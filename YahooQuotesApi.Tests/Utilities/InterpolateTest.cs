using NodaTime;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class InterpolateTest
    {
        private readonly Action<string> Write;
        public InterpolateTest(ITestOutputHelper output) => Write = output.WriteLine;

        [Fact]
        public void TestNotEnoughData()
        {
            var list = new List<PriceTick>();
            Assert.Throws<ArgumentException>(() => list.InterpolateAdjustedClose(new ZonedDateTime().ToInstant()));
            list.Add(new PriceTick(new ZonedDateTime(), 0));
            Assert.Throws<ArgumentException>(() => list.InterpolateAdjustedClose(new ZonedDateTime().ToInstant()));
            list.Add(new PriceTick(new ZonedDateTime(), 0));
            Assert.Equal(0, list.InterpolateAdjustedClose(new ZonedDateTime().ToInstant()));
            list.Add(new PriceTick(new ZonedDateTime(), 0));
            Assert.Equal(0, list.InterpolateAdjustedClose(new ZonedDateTime().ToInstant()));
        }

        [Fact]
        public void BoundaryTest()
        {
            var list = new List<PriceTick>();
            var zdt1 = new LocalDateTime(2000, 1, 1, 0, 0).InUtc();
            var zdt2 = new LocalDateTime(2000, 1, 2, 0, 0).InUtc();

            list.Add(new PriceTick(zdt1, 0));
            list.Add(new PriceTick(zdt2, 0));

            var result = list.InterpolateAdjustedClose(zdt1.ToInstant());
            Assert.False(double.IsNaN(result)); // enough data

            result = list.InterpolateAdjustedClose(zdt1.PlusHours(-7 * 24).ToInstant());
            Assert.True(double.IsNaN(result)); // not enough data

            result = list.InterpolateAdjustedClose(zdt2.ToInstant());
            Assert.False(double.IsNaN(result)); // enough data

            result = list.InterpolateAdjustedClose(zdt2.PlusHours(7 * 24).ToInstant());
            Assert.True(double.IsNaN(result)); // not enough data
        }

        [Fact]
        public void BoundaryLimitTest()
        {
            var list = new List<PriceTick>();
            var zdt1 = new LocalDateTime(2000, 1, 1, 0, 0).InUtc();
            var zdt2 = new LocalDateTime(2000, 1, 2, 0, 0).InUtc();

            list.Add(new PriceTick(zdt1, 1));
            list.Add(new PriceTick(zdt2, 2));

            var result = list.InterpolateAdjustedClose(zdt2.PlusTicks(1).ToInstant());
            Assert.Equal(2, result);

            result = list.InterpolateAdjustedClose(zdt2.PlusHours(7 * 24).ToInstant());
            Assert.True(double.IsNaN(result)); // not enough data
        }


        [Fact]
        public void InterpolateTest1()
        {
            var list = new List<PriceTick>();

            var zdt1 = new LocalDateTime(2000, 1, 1, 0, 0).InUtc();
            var zdt2 = new LocalDateTime(2000, 1, 5, 0, 0).InUtc();

            list.Add(new PriceTick(zdt1, 1));
            list.Add(new PriceTick(zdt2, 2));

            var result = list.InterpolateAdjustedClose(zdt1.Plus(Duration.FromDays(1)).ToInstant());
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
}

using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace YahooQuotesApi
{
    internal static class InterpolateExtensions
    {
        private static readonly Duration FutureLimit = Duration.FromDays(4);
        private static readonly Duration PastLimit = Duration.FromDays(4);

        internal static double InterpolateClose(this IReadOnlyList<PriceTick> list, ZonedDateTime date) =>
            InterpolateClose(list, date.ToInstant());

        private static double InterpolateClose(this IReadOnlyList<PriceTick> list, Instant date) =>
            Interpolate(list, date, x => x.Date.ToInstant(), x => x.Price);

        private static double Interpolate<T>(this IReadOnlyList<T> list, Instant date, Func<T, Instant> getDate, Func<T, double> getValue)
        {
            if (list.Count < 2)
                throw new ArgumentException(nameof(list));

            var firstItem = list[0];
            var firstDate = getDate(firstItem);
            if (date <= firstDate) // not enough data
                return firstDate - date <= PastLimit ? getValue(firstItem) : double.NaN;

            var lastItem = list[list.Count - 1];
            var lastDate = getDate(lastItem);
            if (date >= lastDate)
                return date - lastDate <= FutureLimit ? getValue(lastItem) : double.NaN;

            var p = list.BinarySearch(date, x => getDate(x));

            if (p >= 0) // found
                return getValue(list[p]);

            // not found, use linear interpolation
            p = ~p; // ~p is next highest position in list
            var next = list[p];
            var prev = list[p - 1];
            var t1 = getDate(prev);
            var t2 = getDate(next);
            var v1 = getValue(prev);
            var v2 = getValue(next);
            var rate = v1 + (date - t1) / (t2 - t1) * (v2 - v1);
            return rate;
        }

        internal static int BinarySearch<T>(this IReadOnlyList<T> list, IComparable searchValue, Func<T, IComparable> getComparable)
        {
            if (!list.Any())
                throw new ArgumentException(nameof(list));
            var low = 0;
            var high = list.Count - 1;
            while (low <= high)
            {
                var mid = (high + low) >> 1;
                var result = getComparable(list[mid]).CompareTo(searchValue);
                if (result == 0)
                    return mid;
                if (result < 0)
                    low = mid + 1;
                else
                    high = mid - 1;
            }
            return ~low;
        }
    }
}

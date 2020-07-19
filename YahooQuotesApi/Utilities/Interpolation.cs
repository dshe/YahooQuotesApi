using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace YahooQuotesApi
{
    internal static class InterpolateExtensions
    {
        private static readonly Duration FutureLimit = Duration.FromDays(3);

        internal static double Interpolate(this IReadOnlyList<PriceTick> list, Instant instant)
        {
            if (list.Count < 2)
                throw new ArgumentException(nameof(list));

            if (instant < list[0].Date.ToInstant()) // not enough data
                return double.NaN;

            var last = list[list.Count - 1];
            var future = instant - last.Date.ToInstant();
            if (future >= Duration.Zero)
                return future <= FutureLimit ? last.AdjustedClose : double.NaN;

            var p = list.BinarySearch2(instant, x => x.Date.ToInstant());

            if (p >= 0) // found
                return list[p].AdjustedClose;

            p = ~p; // not found, ~p is next highest position in list; linear interpolation
            var t1 = list[p - 1].Date.ToInstant();
            var t2 = list[p].Date.ToInstant();
            var v1 = list[p - 1].AdjustedClose;
            var v2 = list[p].AdjustedClose;
            var rate = v1 + (instant - t1) / (t2 - t1) * (v2 - v1);
            return rate;
        }

        // This BinarySearch supports IReadOnlyList<T>.
        internal static int BinarySearch2<T>(this IReadOnlyList<T> list, IComparable searchValue, Func<T, IComparable> getComparable)
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

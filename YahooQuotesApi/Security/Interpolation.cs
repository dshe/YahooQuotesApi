using System.Collections.Generic;
using System.Linq;

namespace YahooQuotesApi;

internal static class InterpolateExtensions
{
    private static readonly Duration FutureLimit = Duration.FromDays(4);
    private static readonly Duration PastLimit = Duration.FromDays(4);

    internal static double InterpolateValue(this IReadOnlyList<ValueTick> list, Instant date) =>
        Interpolate(list, date, x => x.Date, x => x.Value);

    private static double Interpolate<T>(this IReadOnlyList<T> list, Instant date, Func<T, Instant> getDate, Func<T, double> getValue)
    {
        if (list.Count < 2)
            throw new ArgumentException("Not enough items.", nameof(list));

        T firstItem = list[0];
        Instant firstDate = getDate(firstItem);
        if (date <= firstDate) // not enough data
            return firstDate - date <= PastLimit ? getValue(firstItem) : double.NaN;

        T lastItem = list[list.Count - 1];
        Instant lastDate = getDate(lastItem);
        if (date >= lastDate)
            return date - lastDate <= FutureLimit ? getValue(lastItem) : double.NaN;

        int p = list.BinarySearch(date, x => getDate(x));

        if (p >= 0) // found
            return getValue(list[p]);

        // not found, use linear interpolation
        p = ~p; // ~p is next highest position in list
        T next = list[p];
        T prev = list[p - 1];
        Instant t1 = getDate(prev);
        Instant t2 = getDate(next);
        double v1 = getValue(prev);
        double v2 = getValue(next);
        double rate = v1 + (date - t1) / (t2 - t1) * (v2 - v1);
        return rate;
    }

    internal static int BinarySearch<T>(this IReadOnlyList<T> list, IComparable searchValue, Func<T, IComparable> getComparable)
    {
        if (!list.Any())
            throw new ArgumentException("No items.", nameof(list));
        int low = 0;
        int high = list.Count - 1;
        while (low <= high)
        {
            int mid = (high + low) >> 1;
            int result = getComparable(list[mid]).CompareTo(searchValue);
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

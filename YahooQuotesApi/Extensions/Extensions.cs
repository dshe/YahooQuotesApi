using System.Collections.Immutable;
using System.Runtime.InteropServices;
namespace YahooQuotesApi;

internal static partial class Extensions
{
    internal static string AsString(this IEnumerable<string> cookies) =>
        Environment.NewLine + string.Join(Environment.NewLine, cookies);

    internal static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class =>
        source.Where(t => t is not null).Cast<T>();

    internal static string? IsIncreasing<T>(this IEnumerable<T> items, Func<T, Instant> compare)
    {
        T[] list = [.. items];
        for (var i = 0; i < list.Length - 1; i++)
        {
            if (compare(list[i]) >= compare(list[i + 1]))
                return $"Items[{list.Length}] [{i}]:{compare(list[i])} >= [{i+1}]:{compare(list[i + 1])}";
        }
        return null;
    }

    internal static bool IsIncreaing<T>(this IEnumerable<T> items, Func<T, T, bool> compare)
    {
        T[] list = [.. items];
        for (var i = 0; i < list.Length - 1; i++)
        {
            if (!compare(list[i], list[i + 1]))
                return false;
        }
        return true;
    }

    internal static T[] AsArray<T>(this ImmutableArray<T> a) =>
    ImmutableCollectionsMarshal.AsArray(a) ?? throw new ArgumentNullException(nameof(a));
    internal static ImmutableArray<T> AsImmutableArray<T>(this T[] a) =>
        ImmutableCollectionsMarshal.AsImmutableArray(a);

    internal static Span<T> AsSpan<T>(this List<T> list) => CollectionsMarshal.AsSpan(list);
    internal static void SetCount<T>(this List<T> list, int x) => CollectionsMarshal.SetCount(list, x);
}


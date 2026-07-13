using System.Collections.Immutable;
using System.Runtime.InteropServices;
namespace YahooQuotesApi;

internal static partial class Extensions
{
    extension(IEnumerable<string> strings)
    {
        internal string AsString() => Environment.NewLine + string.Join(Environment.NewLine, strings);
    }

    extension<T>(ImmutableArray<T> ia)
    {
        internal T[] AsArray() => ImmutableCollectionsMarshal.AsArray(ia) ?? throw new ArgumentNullException(nameof(ia));
    }

    extension<T>(T[] a)
    {
        internal ImmutableArray<T> AsImmutableArray() => ImmutableCollectionsMarshal.AsImmutableArray(a);
    }

    extension<T>(IEnumerable<T> items)
    {
        internal string? IsIncreasing(Func<T, Instant> compare)
        {
            T[] list = [.. items];
            for (int i = 0; i < list.Length - 1; i++)
            {
                if (compare(list[i]) >= compare(list[i + 1]))
                    return $"Items[{list.Length}] [{i}]:{compare(list[i])} >= [{i + 1}]:{compare(list[i + 1])}";
            }
            return null;
        }

        internal bool IsIncreaing(Func<T, T, bool> compare)
        {
            T[] list = [.. items];
            for (int i = 0; i < list.Length - 1; i++)
            {
                if (!compare(list[i], list[i + 1]))
                    return false;
            }
            return true;
        }
    }

}

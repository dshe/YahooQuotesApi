using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace YahooQuotesApi
{
    internal static class Extensions
    {
        internal static string GetRandomString(int length) =>
            Guid.NewGuid().ToString()[..length];

        internal static string ToPascal(this string source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if (source == "")
                return "";
            char[] chars = source.ToCharArray();
            chars[0] = Char.ToUpper(chars[0]);
            return new string(chars);
        }

        internal static string Name<T>(this T source) where T : Enum
        {
            string name = source.ToString();
            if (typeof(T).GetMember(name).First().GetCustomAttribute(typeof(EnumMemberAttribute)) is EnumMemberAttribute attr
                && attr.IsValueSetExplicitly && attr.Value != null)
                name = attr.Value;
            return name;
        }

        internal static IEnumerable<T> NotNull<T>(this IEnumerable<T?> source) where T: class
        {
            foreach (T? item in source)
            {
                if (item != null)
                    yield return item;
            }
        }

        internal static double RoundToSigFigs(this double num, int figs)
        {
            if (num == 0)
                return 0;

            double d = Math.Ceiling(Math.Log10(num < 0 ? -num : num));
            int power = figs - (int)d;

            double magnitude = Math.Pow(10, power);
            double shifted = Math.Round(num * magnitude);
            return shifted / magnitude;
        }

        internal static Uri ToUri(this string url) => new Uri(url);
        internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> items) => new HashSet<T>(items);
    }
}

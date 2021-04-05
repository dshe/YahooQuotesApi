using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace YahooQuotesApi
{
    internal static class ExtensionMethods
    {
        internal static string ToPascal(this string source)
        {
            if (source == null)
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
            if (typeof(T).GetMember(name).First().GetCustomAttribute(typeof(EnumMemberAttribute)) is EnumMemberAttribute attr && attr.IsValueSetExplicitly)
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
    }
}

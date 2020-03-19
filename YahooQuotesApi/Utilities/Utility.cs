using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NodaTime;

namespace YahooQuotesApi
{
    internal static class Utility
    {
        internal static IClock Clock { get; set; } = SystemClock.Instance;

        internal static string GetRandomString(int length) =>
            Guid.NewGuid().ToString().Substring(0, length);

        internal static string CheckSymbol(string symbol)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));
            if (symbol == "" || symbol.Contains(" "))
                throw new ArgumentException(nameof(symbol));
            return symbol.ToUpper();
        }
        internal static IEnumerable<string> CheckSymbols(IEnumerable<string> symbols)
        {
            if (symbols == null)
                throw new ArgumentNullException(nameof(symbols));
            return symbols.Select(s => CheckSymbol(s)).Distinct();
        }
    }

    internal static class ExtensionMethods
    {
        internal static DateTimeZone? ToTimeZoneOrNull(this string name) =>
            DateTimeZoneProviders.Tzdb.GetZoneOrNull(name);
        internal static DateTimeZone ToTimeZone(this string name) =>
            DateTimeZoneProviders.Tzdb.GetZoneOrNull(name) ?? throw new TimeZoneNotFoundException(name);

        internal static ZonedDateTime ToZonedDateTime(this long unixTimeSeconds, DateTimeZone zone) =>
            Instant.FromUnixTimeSeconds(unixTimeSeconds).InZone(zone);

        internal static string ToPascal(this string str)
        {
            if (str.Count() <= 1)
                return str.ToUpper();
            return str.Substring(0, 1).ToUpper() + str.Substring(1);
        }

        internal static string Name<T>(this T @enum) where T : Enum
        {
            string name = @enum.ToString();
            if (typeof(T).GetMember(name).First().GetCustomAttribute(typeof(EnumMemberAttribute)) is EnumMemberAttribute attr && attr.IsValueSetExplicitly)
                name = attr.Value;
            return name;
        }

        internal static List<string> CaseInsensitiveDuplicates(this IEnumerable<string> strings)
        {
            var hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return strings.Where(str => !hashSet.Add(str)).ToList();
        }

        internal static string ToCommaDelimitedList(this IEnumerable<string> strings) =>
            string.Join(", ", strings);
    }
}

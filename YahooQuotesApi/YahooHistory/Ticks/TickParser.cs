using System;
using System.Globalization;
using NodaTime;
using NodaTime.Text;
namespace YahooQuotesApi
{
    internal static class TickParser
    {
        private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

        internal static string GetParamFromType<T>()
        {
            var type = typeof(T);
            if (type == typeof(PriceTick))
                return "history";
            else if (type == typeof(DividendTick))
                return "div";
            else if (type == typeof(SplitTick))
                return "split";

            throw new Exception("GetParamFromType: invalid type.");
        }

        internal static object? Parse(string param, string[] row, LocalTime time, DateTimeZone tz)
        {
            if (param == "history")
                return PriceTick.Create(row, time, tz);
            if (param == "div")
                return DividendTick.Create(row, time, tz);
            if (param == "split")
                return SplitTick.Create(row, time, tz);
            throw new Exception("Parse<T>: invalid type.");
        }

        internal static Instant ToInstant(this string dateStr, LocalTime time, DateTimeZone tz)
        {
            var result = DatePattern.Parse(dateStr);
            var date = result.Success ? result.Value : throw new Exception($"Could not convert '{dateStr}' to LocalDate.", result.Exception);
            return date.At(time).InZoneStrictly(tz).ToInstant();
        }

        internal static decimal ToDecimal(this string str)
        {
            if (str == "null")
                return 0M;

            if (!decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                throw new Exception($"Could not convert '{str}' to Decimal.");

            return result;
        }

        internal static long ToInt64(this string str)
        {
            if (str == "null")
                return 0L;

            if (!long.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out long result))
                throw new Exception($"Could not convert '{str}' to Int64.");

            return result;
        }
    }
}

using System;
using System.Globalization;
using NodaTime;
using NodaTime.Text;

namespace YahooQuotesApi
{
    internal static class TickParser
    {
        private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

        internal static string GetParamFromType<ITick>()
        {
            var type = typeof(ITick);

            if (type == typeof(PriceTick))
                return "history";
            else if (type == typeof(DividendTick))
                return "div";
            else if (type == typeof(SplitTick))
                return "split";

            throw new Exception("GetParamFromType: invalid type.");
        }

        internal static ITick? Parse<ITick>(string[] row) where ITick: class
        {
            var type = typeof(ITick);
            object? instance;

            if (type == typeof(PriceTick))
                instance = PriceTick.Create(row);
            else if (type == typeof(DividendTick))
                instance = DividendTick.Create(row);
            else if (type == typeof(SplitTick))
                instance = SplitTick.Create(row);
            else
                throw new Exception("Parse<ITick>: invalid type.");

            return (ITick?)instance;
        }

        internal static LocalDate ToLocalDate(this string str)
        {
            var result = DatePattern.Parse(str);
            return result.Success ? result.Value : throw new Exception($"Could not convert '{str}' to LocalDate.", result.Exception);
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

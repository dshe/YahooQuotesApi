using NodaTime.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace YahooQuotesApi
{
    internal static class TickParser
    {
        private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

        internal static object[] ToTicks(this Stream stream, Type type)
        {
            var ticks = new List<object>();

            using var streamReader = new StreamReader(stream);
            streamReader.ReadLine(); // read header
            while (!streamReader.EndOfStream)
            {
                var row = streamReader.ReadLine().Split(',');
                var tick = GetTick(row, type);
                if (tick != null)
                    ticks.Add(tick);
            }
            // dividend ticks are returned by Yahoo in random order!
            if (type == typeof(DividendTick))
            {
                return ticks
                    .Cast<DividendTick>()
                    .OrderBy(tick => tick.Date)
                    .Cast<object>()
                    .ToArray();
            }
            return ticks.ToArray();
        }

        private static object? GetTick(string[] row, Type type)
        {
            var result = DatePattern.Parse(row[0]);
            var date = result.Success ? result.Value : throw new Exception($"Could not convert '{row[0]}' to LocalDate.", result.Exception);
            if (type == typeof(CandleTick))
            {
                if (row[5] == "null")
                    return null;
                return new CandleTick(date, row[1].ToDouble(), row[2].ToDouble(), row[3].ToDouble(),
                    row[4].ToDouble(), row[5].ToDouble(), row[6].ToLong());
            }
            if (type == typeof(DividendTick))
                return new DividendTick(date, row[1].ToDouble());
            if (type == typeof(SplitTick))
                return new SplitTick(date, row[1]);
            throw new InvalidOperationException("ticktype");
        }

        internal static double ToDouble(this string str)
        {
            if (str == "null")
                return 0d;

            if (!double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                throw new InvalidDataException($"Could not convert '{str}' to Double.");

            return result.RoundToSigFigs(7);
        }

        internal static long ToLong(this string str)
        {
            if (str == "null")
                return 0L;

            if (!long.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out long result))
                throw new InvalidDataException($"Could not convert '{str}' to Long.");

            return result;
        }
    }
}

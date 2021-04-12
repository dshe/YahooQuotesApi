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

        internal static ITick[] ToTicks<T>(this Stream stream) where T: ITick
        {
            var ticks = new HashSet<ITick>();
            using var streamReader = new StreamReader(stream);
            streamReader.ReadLine(); // read header
            while (!streamReader.EndOfStream)
            {
                var row = streamReader.ReadLine().Split(',');
                var tick = GetTick<T>(row);
                if (tick == null)
                    continue;
                if (!ticks.Add(tick))
                    throw new InvalidDataException("Duplicate tick date: " + tick.ToString() + ".");
            }
            // sometimes ticks are returned in seemingly random order!
            return ticks.OrderBy(x => x.Date).ToArray();
        }

        private static ITick? GetTick<T>(string[] row) where T: ITick
        {
            var result = DatePattern.Parse(row[0]);
            var date = result.Success ? result.Value : throw new InvalidDataException($"Could not convert '{row[0]}' to LocalDate.", result.Exception);
            var type = typeof(T);
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

            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result.RoundToSigFigs(7);

            throw new InvalidDataException($"Could not convert '{str}' to Double.");
        }

        internal static long ToLong(this string str)
        {
            if (str == "null")
                return 0L;

            if (long.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out long result))
                return result;

            throw new InvalidDataException($"Could not convert '{str}' to Long.");
        }
    }
}

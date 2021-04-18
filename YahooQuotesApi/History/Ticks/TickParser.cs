using NodaTime;
using NodaTime.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    internal static class TickParser
    {
        private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

        internal static async Task<ITick[]> ToTicks<T>(this StreamReader streamReader) where T: ITick
        {
            var ticks = new HashSet<ITick>();
            // read header
            await streamReader.ReadLineAsync().ConfigureAwait(false); 
            while (!streamReader.EndOfStream)
            {
                var row = await streamReader.ReadLineAsync().ConfigureAwait(false);
                var tick = GetTick<T>(row.Split(','));
                if (tick == null)
                    continue;
                if (!ticks.Add(tick))
                    throw new InvalidDataException("Duplicate tick date: " + tick.ToString() + ".");
            }
            // occasionally, ticks are returned in seemingly random order!
            return ticks.OrderBy(x => x.Date).ToArray();
        }

        private static ITick? GetTick<T>(string[] row) where T : ITick
        {
            var date = row[0].ToDate();
            if (typeof(T) == typeof(CandleTick))
            {
                if (row[5] == "null")
                    return null;
                return new CandleTick(date, row[1].ToDouble(), row[2].ToDouble(), row[3].ToDouble(),
                    row[4].ToDouble(), row[5].ToDouble(), row[6].ToLong());
            }
            if (typeof(T) == typeof(DividendTick))
                return new DividendTick(date, row[1].ToDouble());
            if (typeof(T) == typeof(SplitTick))
            {
                var split = row[1].Split(new[] { ':', '/' });
                if (split.Length != 2)
                    throw new Exception("Split separator not found.");
                return new SplitTick(date, split[1].ToDouble(), split[0].ToDouble());
            }
            throw new InvalidOperationException("ticktype");
        }

        internal static LocalDate ToDate(this string str)
        {
            var result = DatePattern.Parse(str);
            if (result.Success)
                return result.Value;

            throw new InvalidDataException($"Could not convert '{str}' to LocalDate.", result.Exception);
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

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
            HashSet<ITick> ticks = new();
            // read header
            await streamReader.ReadLineAsync().ConfigureAwait(false); 
            while (!streamReader.EndOfStream)
            {
                string? row = await streamReader.ReadLineAsync().ConfigureAwait(false);
                if (row == null)
                    continue;
				ITick? tick = GetTick<T>(row.Split(','));
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
            LocalDate date = row[0].ToDate();
            if (typeof(T) == typeof(PriceTick))
            {
                if (row[5] == "null")
                    return null;
                return new PriceTick
                {
                    Date = date,
                    Open = row[1].ToDouble(),
                    High = row[2].ToDouble(),
                    Low = row[3].ToDouble(),
                    Close = row[4].ToDouble(),
                    AdjustedClose = row[5].ToDouble(),
                    Volume = row[6].ToLong()
                };
            }
            if (typeof(T) == typeof(DividendTick))
                return new DividendTick { Date = date, Dividend = row[1].ToDouble() };
            if (typeof(T) == typeof(SplitTick))
            {
                string[] split = row[1].Split(new[] { ':', '/' });
                if (split.Length != 2)
                    throw new Exception("Split separator not found.");
                return new SplitTick { Date = date, BeforeSplit = split[1].ToDouble(), AfterSplit = split[0].ToDouble() };

            }
            throw new InvalidOperationException("Tick type.");
        }

        internal static LocalDate ToDate(this string str)
        {
            ParseResult<LocalDate> result = DatePattern.Parse(str);
            if (result.Success)
                return result.Value;

            throw new InvalidDataException($"Could not convert '{str}' to LocalDate.", result.Exception);
        }

        internal static double ToDouble(this string str)
        {
            if (str == "null")
                return 0d;

            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
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

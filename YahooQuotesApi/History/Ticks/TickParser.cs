using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NodaTime;
using NodaTime.Text;
using CsvHelper;

namespace YahooQuotesApi
{
    internal static class TickParser
    {
        private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

        internal static List<object> GetTicks(StreamReader streamReader, Type type, LocalTime? closeTime, DateTimeZone? tz)
        {
            using var reader = new CsvReader(streamReader, CultureInfo.InvariantCulture);
            if (!reader.Read()) // skip headers
                throw new Exception("Could not read csv headers.");
            var ticks = new List<object>();
            while (reader.Read())
            {
                var row = reader.Context.Record;
                var dateStr = row[0];
                var result = DatePattern.Parse(dateStr);
                var date = result.Success ? result.Value : throw new Exception($"Could not convert '{dateStr}' to LocalDate.", result.Exception);
                object tick;
                if (type == typeof(PriceTick))
                {
                    if (closeTime == null)
                        throw new ArgumentNullException(nameof(closeTime));
                    if (tz == null)
                        throw new ArgumentNullException(nameof(tz));
                    var zdt = date.At(closeTime.Value).InZoneStrictly(tz);
                    tick = new PriceTick(zdt, row);
                }
                else if (type == typeof(DividendTick))
                    tick = new DividendTick(date, row[1]);
                else if (type == typeof(SplitTick))
                    tick = new SplitTick(date, row[1]);
                else
                    throw new Exception("Invalid tick type");
                ticks.Add(tick);
            }
            // dividend ticks are returned by Yahoo in random order!
            if (type == typeof(DividendTick))
            {
                return ticks.Cast<DividendTick>()
                    .OrderBy(tick => tick.Date)
                    .Cast<object>()
                    .ToList();
            }
            return ticks;
        }

        internal static double ToDouble(this string str)
        {
            if (str == "null")
                return 0d;

            if (!double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                throw new Exception($"Could not convert '{str}' to Decimal.");

            return result;
        }

        internal static long ToLong(this string str)
        {
            if (str == "null")
                return 0L;

            if (!long.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out long result))
                throw new Exception($"Could not convert '{str}' to Int64.");

            return result;
        }
    }
}

using NodaTime;
using System;

namespace YahooQuotesApi
{
    public sealed class SplitTick
    {
        public Instant Date { get; }
        public decimal BeforeSplit { get;  }
        public decimal AfterSplit { get; }

        private SplitTick(string[]row, LocalTime time, DateTimeZone tz)
        {
            Date = row[0].ToInstant(time, tz);
            var split = row[1].Split(new[] { ':', '/' });
            if (split.Length != 2)
                throw new Exception("Split separator not found");
            AfterSplit = split[0].ToDecimal();
            BeforeSplit = split[1].ToDecimal();
        }

        internal static SplitTick? Create(string[] row, LocalTime time, DateTimeZone tz)
        {
            var tick = new SplitTick(row, time, tz);

            if (tick.AfterSplit == 0 && tick.BeforeSplit == 0)
                return null;

            return tick;
        }
    }
}

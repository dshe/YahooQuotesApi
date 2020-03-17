using NodaTime;
using System.Diagnostics;

namespace YahooQuotesApi
{
    public sealed class DividendTick
    {
        public Instant Date { get; }
        public decimal Dividend { get; }

        private DividendTick(string[] row, LocalTime time, DateTimeZone tz)
        {
            Debug.Assert(row.Length == 2);

            Date = row[0].ToInstant(time, tz);
            Dividend = row[1].ToDecimal();
        }

        internal static DividendTick? Create(string[] row, LocalTime time, DateTimeZone tz)
        {
            var tick = new DividendTick(row, time, tz);

            if (tick.Dividend == 0)
                return null;

            return tick;
        }
    }
}

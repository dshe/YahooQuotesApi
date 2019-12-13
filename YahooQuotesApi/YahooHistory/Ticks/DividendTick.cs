using NodaTime;
using System.Diagnostics;

namespace YahooQuotesApi
{
    public sealed class DividendTick : ITick
    {
        public LocalDate Date { get; }
        public decimal Dividend { get; }

        private DividendTick(string[] row)
        {
            Debug.Assert(row.Length == 2);

            Date = row[0].ToLocalDate();
            Dividend = row[1].ToDecimal();
        }

        internal static DividendTick? Create(string[] row, bool ignoreEmptyRows)
        {
            var tick = new DividendTick(row);

            if (ignoreEmptyRows && tick.Dividend == 0)
                return null;

            return tick;
        }
    }
}

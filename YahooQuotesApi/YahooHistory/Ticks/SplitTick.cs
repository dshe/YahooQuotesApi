using NodaTime;
using System;

namespace YahooQuotesApi
{
    public sealed class SplitTick : ITick
    {
        public LocalDate Date { get; }
        public decimal BeforeSplit { get;  }
        public decimal AfterSplit { get; }

        private SplitTick(string[]row)
        {
            Date = row[0].ToLocalDate();
            var split = row[1].Split(new[] { ':', '/' });
            if (split.Length != 2)
                throw new Exception("Split separator not found");
            AfterSplit = split[0].ToDecimal();
            BeforeSplit = split[1].ToDecimal();
        }

        internal static SplitTick? Create(string[] row)
        {
            var tick = new SplitTick(row);

            if (tick.AfterSplit == 0 && tick.BeforeSplit == 0)
                return null;

            return tick;
        }
    }
}

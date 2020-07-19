using System;
using NodaTime;

namespace YahooQuotesApi
{
    public sealed class SplitTick
    {
        public LocalDate Date { get; }
        public double BeforeSplit { get;  }
        public double AfterSplit { get; }

        internal SplitTick(LocalDate date, string str)
        {
            Date = date;
            var split = str.Split(new[] { ':', '/' });
            if (split.Length != 2)
                throw new Exception("Split separator not found.");
            AfterSplit = split[0].ToDouble();
            BeforeSplit = split[1].ToDouble();
        }

        public override string ToString() => $"{Date}, {BeforeSplit}, {AfterSplit}";
    }
}

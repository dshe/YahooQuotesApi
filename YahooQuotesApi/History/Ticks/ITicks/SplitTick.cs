using NodaTime;

namespace YahooQuotesApi
{
    public sealed class SplitTick : ITick
    {
        public LocalDate Date { get; }
        public double BeforeSplit { get; }
        public double AfterSplit { get; }

        internal SplitTick(LocalDate date, double before, double after)
        {
            Date = date;
            BeforeSplit = before;
            AfterSplit = after;
        }

        public override string ToString() => $"{Date} {BeforeSplit}:{AfterSplit}";
    }
}

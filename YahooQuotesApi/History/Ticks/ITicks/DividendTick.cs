using NodaTime;

// ex-dividend date

namespace YahooQuotesApi
{
    public sealed class DividendTick : ITick
    {
        public LocalDate Date { get; }
        public double Dividend { get; }

        internal DividendTick(LocalDate date, double dividend)
        {
            Date = date;
            Dividend = dividend;
        }

        public override string ToString() => $"{Date}, {Dividend}";
    }
}

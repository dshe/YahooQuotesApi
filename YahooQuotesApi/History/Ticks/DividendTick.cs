using NodaTime;

namespace YahooQuotesApi
{
    public sealed class DividendTick
    {
        public LocalDate Date { get; }
        public double Dividend { get; }

        internal DividendTick(LocalDate date, string str)
        {
            Date = date;
            Dividend = str.ToDouble();
        }

        public override string ToString() => $"{Date}, {Dividend}";
    }
}

using NodaTime;

// ex-dividend date

namespace YahooQuotesApi
{
    public sealed class DividendTick : ITick
    {
        public LocalDate Date { get; init; }
        public double Dividend { get; init; }

        public override string ToString() => $"{Date}, {Dividend}";
    }
}

using NodaTime;

namespace YahooQuotesApi
{
    public readonly struct RateTick
    {
        public ZonedDateTime Date { get; }

        public double Rate { get; }

        internal RateTick(ZonedDateTime date, double rate) => (Date, Rate) = (date, rate);

        public override string ToString() => $"{Date}, {Rate}";
    }
}

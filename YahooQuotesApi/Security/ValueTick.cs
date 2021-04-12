using NodaTime;

namespace YahooQuotesApi
{
    public sealed class ValueTick
    {
        public Instant Date { get; }
        public double Value { get; } // this is adjusted close from Yahoo Finance
        public long Volume { get; }

        public ValueTick(Instant date, double price, long volume = 0)
            => (Date, Value, Volume) = (date, price, volume);

        public ValueTick(CandleTick tick, LocalTime close, DateTimeZone tz, bool useNonAdjustedClose)
        {
            Date = tick.Date.At(close).InZoneLeniently(tz).ToInstant();
            Value = useNonAdjustedClose ? tick.Close : tick.AdjustedClose;
            Volume = tick.Volume;
        }

        public override string ToString() => $"{Date}, {Value}, {Volume}";
    }
}

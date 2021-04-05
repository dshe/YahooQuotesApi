using NodaTime;
using System;

namespace YahooQuotesApi
{
    public sealed class PriceTick
    {
        public ZonedDateTime Date { get; }
        public double Price { get; } // this is adjusted close from Yahoo Finance
        public long Volume { get; }

        public PriceTick(ZonedDateTime date, double price, long volume = 0)
            => (Date, Price, Volume) = (date, price, volume);

        public PriceTick(CandleTick tick, LocalTime close, DateTimeZone tz, bool useNonAdjustedClose)
        {
            Date = tick.Date.At(close).InZoneLeniently(tz);
            if (useNonAdjustedClose)
            {
                Price = tick.Close;
                Volume = tick.Volume;
                return;
            }
            Price = tick.AdjustedClose;
            Volume = Convert.ToInt64(tick.Volume * tick.Close / tick.AdjustedClose);
        }

        public override string ToString() => $"{Date}, {Price}, {Volume}";
    }
}

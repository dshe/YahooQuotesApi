using NodaTime;

/* AAPL: Nasdaq Official Closing Price (NOCP)
02/19/2021 $129.87 (official) => 129.869995 (text from Yahoo)
It seems like the text values were floats converted to double.
So to get the original value:

float f = float.Parse("129.869995");
decimal dec = new decimal(f);
double d = Convert.ToDouble(dec);
(this adjustment is not performed)

Yahoo Finance prices are adjusted for splits.
Yahoo Finance "Adjusted" price is adjusted for dividends.
*/
namespace YahooQuotesApi
{
    public sealed class PriceTick : ITick
    {
        public LocalDate Date { get; }
        public double Open { get; }
        public double High { get; }
        public double Low { get; }
        public double Close { get; }
        public double AdjustedClose { get; }
        public long Volume { get; }

        internal PriceTick(LocalDate date, double open, double high, double low, double close, double adjustedClose, long volume)
        {
            Date = date;
            Open = open;
            High = high;
            Low  = low;
            Close = close;
            AdjustedClose = adjustedClose;
            Volume = volume;
        }

        public override string ToString() => $"{Date}, {Open}, {High}, {Low}, {Close}, {AdjustedClose}, {Volume}";
    }
}

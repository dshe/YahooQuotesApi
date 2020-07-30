using System;
using System.Collections.Generic;
using NodaTime;

namespace YahooQuotesApi
{
    public sealed class PriceTick
    {
        public ZonedDateTime Date { get; }
        public double Open { get; }
        public double High { get; }
        public double Low { get; }
        public double Close { get; }
        public double AdjustedClose { get; }
        public long Volume { get; }

        internal PriceTick(ZonedDateTime date, string[] row)
        {
            Date = date;
            Open = row[1].ToDouble();
            High = row[2].ToDouble();
            Low  = row[3].ToDouble();
            Close = row[4].ToDouble();
            AdjustedClose = row[5].ToDouble();
            Volume = row[6].ToLong();
        }

        internal PriceTick(ZonedDateTime date)
        {
            Date = date;
            Open = 1;
            High = 1;
            Low = 1;
            Close = 1;
            AdjustedClose = 1;
            Volume = 0;
        }

        internal PriceTick(PriceTick tick, double rate, bool invert = false)
        {
            Date = tick.Date;
            Volume = tick.Volume;

            Open  = tick.Open;
            High  = tick.High;
            Low   = tick.Low;
            Close = tick.Close;
            AdjustedClose = tick.AdjustedClose;

            if (invert)
            {
                Open =  1 / Open;
                High =  1 / High;
                Low =   1 / Low;
                Close = 1 / Close;
                AdjustedClose = 1 / AdjustedClose;
            }

            Open  *= rate;
            High  *= rate;
            Low   *= rate;
            Close *= rate;
            AdjustedClose *= rate;
        }

        internal PriceTick(IDictionary<string, dynamic> dict, double rate)
        {
            Date = dict["RegularMarketTime"];

            Open = GetDouble("RegularMarketOpen");
            High = GetDouble("RegularMarketDayHigh");
            Low = GetDouble("RegularMarketDayLow");
            AdjustedClose = Close = GetDouble("RegularMarketPrice");
            
            Volume = Convert.ToInt64(GetValueElseZero("RegularMarketVolume"));
            
            double GetDouble(string fieldName)
            {
                var value = GetValueElseZero(fieldName);
                return Convert.ToDouble(value) * rate;
            }
            dynamic GetValueElseZero(string fieldName)
            {
                if (!dict.TryGetValue(fieldName, out var value))
                    return 0;
                return value;
            }
        }

        // used for testing
        internal PriceTick(ZonedDateTime date, double adjustedClose) => (Date, AdjustedClose) = (date, adjustedClose);

        public override string ToString() => $"{Date}, {Open}, {High}, {Low}, {Close}, {AdjustedClose}, {Volume}";
    }
}

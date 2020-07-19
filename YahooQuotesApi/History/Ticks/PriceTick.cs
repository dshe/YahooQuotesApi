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

        internal PriceTick(PriceTick tick, double rate)
        {
            Date = tick.Date;

            Open = tick.Open * rate;
            High = tick.High * rate;
            Low = tick.Low * rate;
            Close = tick.Close * rate;
            AdjustedClose = tick.AdjustedClose * rate;

            Volume = tick.Volume;
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

using NodaTime;
using System;
using System.Collections.Generic;

namespace YahooQuotesApi
{
    public sealed class Currency
    {
        public string Symbol { get; }
        public string Name { get; }
        public List<RateTick> Rates { get; internal set; } = new List<RateTick>();
        internal Currency(string symbol, string name)
        {
            Symbol = symbol;
            Name = name;
        }
    }

    public readonly struct RateTick : IComparable<RateTick>
    {
        public Instant Date { get; }
        // Use double rather than decimal because currency prices are not official,
        // and may require calculation to get cross rates.
        public double Rate { get; } 
        internal RateTick(Instant date, double rate)
        {
            Date = date;
            Rate = rate;
        }
        // used for BinarySearch
        public int CompareTo(RateTick other) => Date.CompareTo(other.Date);
    }
}

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

    public sealed class RateTick : IComparable<RateTick>
    {
        public LocalDate Date { get; }
        public decimal Rate { get; }
        internal RateTick(LocalDate date, decimal rate)
        {
            Date = date;
            Rate = rate;
        }
        // used for BinarySearch
        public int CompareTo(RateTick other) => Date.CompareTo(other.Date);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;

namespace YahooQuotesApi
{
    internal static class Utility
    {
        internal static IClock Clock { get; set; } = SystemClock.Instance;

        internal static string GetRandomString(int length) =>
            Guid.NewGuid().ToString().Substring(0, length);
    }

    internal struct Symbol : IComparable
    {
        internal string Name { get; }
        internal Symbol(string name)
        {
            if (name.Any(char.IsWhiteSpace))
                throw new ArgumentException($"Symbol name: '{name}'.");
            Name = name.ToUpper();
        }

        internal bool IsEmpty => Name == "";
        internal bool IsCurrency => Name.EndsWith("=X") && Name.Length == 5;
        internal bool IsCurrencyRate => Name.EndsWith("=X") && Name.Length == 7;

        internal string Currency => (IsCurrency || IsCurrencyRate) ? Name.Substring(0, 3) :
            throw new InvalidOperationException("Symbol is not a currency or rate.");

        internal string BaseCurrency => IsCurrencyRate ? Name.Substring(3, 3) :
            throw new InvalidOperationException("Symbol is not a currency rate.");

        public int CompareTo(object obj) => Name.CompareTo(obj);

        public override string ToString() => Name;
    }
}

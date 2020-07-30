using System;
using System.Collections.Generic;
using System.Linq;

namespace YahooQuotesApi
{
    internal readonly struct Symbol : IComparable<Symbol>, IEquatable<Symbol>
    {
        internal string Name { get; }

        internal Symbol(string name)
        {
            if (name.Any(char.IsWhiteSpace))
                throw new ArgumentException($"Symbol name: '{name}'.");
            Name = name.ToUpper();
        }

        internal bool IsEmpty => string.IsNullOrEmpty(Name);
        internal bool IsCurrency => Name.EndsWith("=X") && Name.Length == 5;
        internal bool IsCurrencyRate => Name.EndsWith("=X") && Name.Length == 7;
        internal string Currency => (IsCurrency || IsCurrencyRate) ? Name.Substring(0, 3) :
            throw new InvalidOperationException("Symbol is not a currency or rate.");
        internal string BaseCurrency => IsCurrencyRate ? Name.Substring(3, 3) :
            throw new InvalidOperationException("Symbol is not a currency rate.");

        public override bool Equals(object? obj) => obj is Symbol symbol && Name == symbol.Name;
        public bool Equals(Symbol other) => Name == other.Name;
        public static bool operator ==(Symbol left, Symbol right) => Equals(left, right);
        public static bool operator !=(Symbol left, Symbol right) => !Equals(left, right);
        public int CompareTo(Symbol other) => Name.CompareTo(other.Name);
        public override int GetHashCode() => 539060726 + EqualityComparer<string>.Default.GetHashCode(Name);
        public override string ToString() => Name;
    }
}

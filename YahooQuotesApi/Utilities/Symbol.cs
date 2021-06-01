using System;
using System.Collections.Generic;
using System.Linq;

namespace YahooQuotesApi
{
    public sealed class Symbol : IComparable<Symbol>, IEquatable<Symbol>
    {
        public static Symbol? TryCreate(string name, bool allowEmpty = false)
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));
            if (name == "" && !allowEmpty || name.Any(char.IsWhiteSpace))
                return null;
            if (name.Count(c => c == '.') > 1 || name.Count(c => c == '=') > 1)
                return null;
            name = name.ToUpper();
            if (name.Contains("=X"))
            {
                if (!name.EndsWith("=X")
                    || (name.Length != 5 && name.Length != 8)
                    || (name.Length == 8 && name.Substring(0, 3) == name.Substring(3, 3)))
                    return null;
            }
            return new Symbol(name);
        }

        public static Symbol Empty { get; } = new Symbol("");

        private Symbol(string name) => this.name = name;

        private readonly string name;

        public string Name
        {
            get
            {
                if (name == "")
                    throw new InvalidOperationException("Symbol is empty.");
                return name;
            }
        }

        public string Suffix
        {
            get
            {
                int pos = Name.IndexOf('.');
                if (pos == -1 || name.EndsWith("."))
                    return "";
                return name[(pos + 1)..];
            }
        }

        public bool IsEmpty => name == "";
        public bool IsCurrency => name != "" && name.Length == 5 && name.EndsWith("=X");
        public bool IsCurrencyRate => name != "" && name.Length == 8 && name.EndsWith("=X");
        public bool IsStock => name != "" && !name.EndsWith("=X");
        public string Currency {
            get
            {
                if (IsCurrency)
                    return name.Substring(0, 3);
                if (IsCurrencyRate)
                    return name.Substring(3, 3);
                throw new InvalidOperationException("Symbol is neither currency nor currency rate.");
            }
        }
        public override bool Equals(object? obj) => obj is Symbol symbol && string.Equals(name, symbol.name);
        public bool Equals(Symbol? other) => string.Equals(name, other?.name);
        public static bool operator ==(Symbol? left, Symbol? right) => Equals(left?.name, right?.name);
        public static bool operator !=(Symbol? left, Symbol? right) => !Equals(left?.name, right?.name);
        public int CompareTo(Symbol? other) => name.CompareTo(other?.name);
        public override int GetHashCode() => 539060726 + EqualityComparer<string?>.Default.GetHashCode(name);
        public override string ToString() => name;
    }

    public static class SymbolExtensions
    {
        public static Symbol ToSymbol(this string name, bool allowEmpty = false) =>
            Symbol.TryCreate(name, allowEmpty) ?? throw new ArgumentException($"Invalid symbol format: '{name}'.");

        public static List<Symbol> ToSymbols(this IEnumerable<string> names, bool allowEmpty = false)
        {
            if (names is null)
                throw new ArgumentNullException(nameof(names));
            return names
                .Select(name => name.ToSymbol(allowEmpty))
                .Distinct()
                .ToList();
        }
    }
}

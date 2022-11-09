using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YahooQuotesApi;

public readonly struct Symbol : IEquatable<Symbol>, IComparable<Symbol>
{
    private readonly string? name; // may be null
    private Symbol(string name) => this.name = name;
    internal static Symbol Undefined { get; }
    public static bool TryCreate(string name, out Symbol symbol)
    {
        ArgumentNullException.ThrowIfNull(name);
        symbol = Undefined;
        if (name.Length == 0 || name.Any(char.IsWhiteSpace))
            return false;
        if (name.Count(c => c == '.') > 1 || name.Count(c => c == '=') > 1)
            return false;
        name = name.ToUpper(CultureInfo.InvariantCulture);
        if (name.Contains("=X", StringComparison.OrdinalIgnoreCase))
        {
            if (!name.EndsWith("=X", StringComparison.OrdinalIgnoreCase)
                || (name.Length != 5 && name.Length != 8)
                || (name.Length == 8 && name[0..3] == name[3..6]))
                return false;
        }
        symbol = new Symbol(name);
        return true;
    }

    public string Name {
        get {
            if (IsValid)
                return name!;
            throw new InvalidOperationException("Undefined symbol.");
        }
    }

    public string Suffix {
        get {
            int pos = Name.IndexOf('.', StringComparison.Ordinal);
            if (pos == -1 || Name.EndsWith(".", StringComparison.Ordinal))
                return "";
            return Name[(pos + 1)..];
        }
    }

    public bool IsValid => name is not null && name.Length != 0;
    public bool IsCurrency => Name.Length == 5 && Name.EndsWith("=X", StringComparison.OrdinalIgnoreCase);
    public bool IsCurrencyRate => Name.Length == 8 && Name.EndsWith("=X", StringComparison.OrdinalIgnoreCase);
    public bool IsStock => Name.Length > 0 && !Name.EndsWith("=X", StringComparison.OrdinalIgnoreCase);
    public string Currency {
        get {
            if (IsCurrency)
                return Name[..3];
            if (IsCurrencyRate)
                return Name[3..6];
            throw new InvalidOperationException("Symbol is neither currency nor currency rate.");
        }
    }
    public override string ToString() => Name;
    public override int GetHashCode() => name is null ? 0 : EqualityComparer<string>.Default.GetHashCode(name);
    public bool Equals(Symbol other) => string.Equals(name, other.name, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is Symbol symbol && Equals(symbol);
    public int CompareTo(Symbol other) => string.CompareOrdinal(name, other.name);
    public static bool operator ==(Symbol left, Symbol right) => left.Equals(right);
    public static bool operator !=(Symbol left, Symbol right) => !(left == right);
    public static bool operator <(Symbol left, Symbol right) => left.CompareTo(right) < 0;
    public static bool operator <=(Symbol left, Symbol right) => left.CompareTo(right) <= 0;
    public static bool operator >(Symbol left, Symbol right) => left.CompareTo(right) > 0;
    public static bool operator >=(Symbol left, Symbol right) => left.CompareTo(right) >= 0;
}

public static class SymbolExtensions
{
    public static Symbol ToSymbol(this string name, bool throwOnFailure = true)
    {
        if (Symbol.TryCreate(name, out Symbol symbol))
            return symbol;
        if (throwOnFailure)
            throw new ArgumentException($"Could not convert '{name}' to Symbol.");
        return Symbol.Undefined;
    }
}

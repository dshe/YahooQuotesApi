using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace YahooQuotesApi;

public struct Symbol : IEquatable<Symbol>, IComparable<Symbol>
{
    public static Symbol TryCreate(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (name.Length == 0 || name.Any(char.IsWhiteSpace))
            return Undefined;
        if (name.Count(c => c == '.') > 1 || name.Count(c => c == '=') > 1)
            return Undefined;
        name = name.ToUpper(CultureInfo.InvariantCulture);
        if (name.Contains("=X", StringComparison.OrdinalIgnoreCase))
        {
            if (!name.EndsWith("=X", StringComparison.OrdinalIgnoreCase)
                || (name.Length != 5 && name.Length != 8)
                || (name.Length == 8 && name[0..3] == name[3..6]))
                return Undefined;
        }
        return new Symbol(name);
    }

    internal static readonly Symbol Undefined = new("");

    private Symbol(string name) => this.name = name;
    private readonly string name;

    public string Name {
        get {
            if (name.Length == 0)
                throw new InvalidOperationException("Undefined symbol.");
            return name;
        }
    }

    public string Suffix {
        get {
            int pos = Name.IndexOf('.', StringComparison.Ordinal);
            if (pos == -1 || name.EndsWith(".", StringComparison.Ordinal))
                return "";
            return name[(pos + 1)..];
        }
    }

    public bool IsCurrency => Name.Length == 5 && name.EndsWith("=X", StringComparison.OrdinalIgnoreCase);
    public bool IsCurrencyRate => Name.Length == 8 && name.EndsWith("=X", StringComparison.OrdinalIgnoreCase);
    public bool IsStock => Name.Length > 0 && !name.EndsWith("=X", StringComparison.OrdinalIgnoreCase);
    public bool IsValid => name.Length != 0;
    public string Currency {
        get {
            if (IsCurrency)
                return name[..3];
            if (IsCurrencyRate)
                return name[3..6];
            throw new InvalidOperationException("Symbol is neither currency nor currency rate.");
        }
    }
    public override string ToString() => name;

    public override int GetHashCode() => EqualityComparer<string>.Default.GetHashCode(name);
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
    public static Symbol ToSymbol(this string name)
    {
        Symbol symbol = Symbol.TryCreate(name);
        if (symbol.IsValid)
            return symbol;
        throw new ArgumentException($"Could not convert '{name}' to Symbol.");
    }
}

using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YahooQuotesApi;

public class Symbol : IEquatable<Symbol>, IComparable<Symbol>
{
    public static Symbol? TryCreate(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (name.Length == 0 || name.Any(char.IsWhiteSpace))
            return null;
        if (name.Count(c => c == '.') > 1 || name.Count(c => c == '=') > 1)
            return null;
        name = name.ToUpper(CultureInfo.InvariantCulture);
        if (name.Contains("=X", StringComparison.OrdinalIgnoreCase))
        {
            if (!name.EndsWith("=X", StringComparison.OrdinalIgnoreCase)
                || (name.Length != 5 && name.Length != 8)
                || (name.Length == 8 && name[0..3] == name[3..6]))
                return null;
        }
        return new Symbol(name);
    }

    public static readonly Symbol Undefined = new("");

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

    public bool IsCurrency => name.Length == 5 && name.EndsWith("=X", StringComparison.OrdinalIgnoreCase);
    public bool IsCurrencyRate => name.Length == 8 && name.EndsWith("=X", StringComparison.OrdinalIgnoreCase);
    public bool IsStock => name.Length > 0 && !name.EndsWith("=X", StringComparison.OrdinalIgnoreCase);
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

    public bool Equals(Symbol? other) => other is Symbol symbol && string.Equals(name, symbol.name, StringComparison.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as Symbol);
    public override int GetHashCode() => 539060726 + EqualityComparer<string?>.Default.GetHashCode(name);
    public int CompareTo(Symbol? other) => string.CompareOrdinal(name, other?.name);

    public static bool operator ==(Symbol left, Symbol right) => (left is null) ? right is null : left.Equals(right);
    public static bool operator !=(Symbol left, Symbol right) => !(left == right);
    public static bool operator <(Symbol left, Symbol right) => left is null ? right is not null : left.CompareTo(right) < 0;
    public static bool operator <=(Symbol left, Symbol right) => left is null || left.CompareTo(right) <= 0;
    public static bool operator >(Symbol left, Symbol right) => left is not null && left.CompareTo(right) > 0;
    public static bool operator >=(Symbol left, Symbol right) => left is null ? right is null : left.CompareTo(right) >= 0;
}

public static class SymbolExtensions
{
    public static Symbol ToSymbol(this string name) => Symbol.TryCreate(name) ??
        throw new ArgumentException($"Could not convert '{name}' to Symbol.");
}

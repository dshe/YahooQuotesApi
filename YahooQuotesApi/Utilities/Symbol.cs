namespace YahooQuotesApi;

public readonly struct Symbol : IEquatable<Symbol>, IComparable<Symbol>
{
    // 2 states: uninitialized or invalid(default, name is null), ok(name is not null)
    // The default value of a struct is the value produced when all of its fields equal their default values.
    // This struct has only the string field 'name'.
    // String is a reference type with default value of null.
    private readonly string? _name;
    private Symbol(string name) => _name = name;

    public string Name
    {
        get
        {
            ArgumentNullException.ThrowIfNull(_name, nameof(_name));
            return _name;
        }
    }

    public string Suffix
    {
        get
        {
            if (_name is null) 
                return "";
            int pos = _name.IndexOf('.', StringComparison.Ordinal);
            if (pos == -1 || pos == _name.Length - 1)
                return "";
            return _name[(pos + 1)..];
        }
    }

    public bool IsValid => _name is not null;
    public bool IsCurrency => _name is not null && _name.Length == 5 && _name.EndsWith("=X", StringComparison.Ordinal);
    public bool IsCurrencyRate => _name is not null && _name.Length == 8 && _name.EndsWith("=X", StringComparison.Ordinal);
    public bool IsSecurity => _name is not null && !_name.EndsWith("=X", StringComparison.Ordinal);

    public string Currency => IsCurrency ? Name[..3] : throw new InvalidOperationException("Symbol is not a currency.");
    public string BaseCurrency => IsCurrencyRate ? Name[..3] : throw new InvalidOperationException("Symbol is not a currency rate.");
    public string QuoteCurrency => IsCurrencyRate ? Name[3..6] : throw new InvalidOperationException("Symbol is not a currency rate.");

    public override string ToString() => _name ?? "<invalid symbol>";
    public override int GetHashCode() => _name is null ? 0 : StringComparer.Ordinal.GetHashCode(_name);
    public bool Equals(Symbol other) => string.Equals(_name, other._name, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is Symbol symbol && Equals(symbol);
    public int CompareTo(Symbol other) => string.CompareOrdinal(_name, other._name);
    public static bool operator ==(Symbol left, Symbol right) => left.Equals(right);
    public static bool operator !=(Symbol left, Symbol right) => !(left == right);
    public static bool operator <(Symbol left, Symbol right) => left.CompareTo(right) < 0;
    public static bool operator <=(Symbol left, Symbol right) => left.CompareTo(right) <= 0;
    public static bool operator >(Symbol left, Symbol right) => left.CompareTo(right) > 0;
    public static bool operator >=(Symbol left, Symbol right) => left.CompareTo(right) >= 0;

    public static Symbol Create(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (TryCreate(name, out Symbol symbol))
            return symbol;
        throw new ArgumentException($"Could not convert '{name}' to Symbol.");
    }

    public static bool TryCreate(string name, out Symbol symbol)
    {
        ArgumentNullException.ThrowIfNull(name);
        symbol = default;
        if (name.Length == 0)
            return false;

        bool hasDot = false, hasEq = false;
        foreach (char c in name)
        {
            if (char.IsWhiteSpace(c))
                return false;
            if (c == '.')
            {
                if (hasDot)
                    return false;
                hasDot = true;
            }
            if (c == '=')
            {
                if (hasEq)
                    return false;
                hasEq = true;
            }
        }

        name = name.ToUpperInvariant();
        if (name.Contains("=X", StringComparison.Ordinal))
        {
            if (!name.EndsWith("=X", StringComparison.Ordinal)
                || (name.Length != 5 && name.Length != 8)
                || (name.Length == 8 && string.Equals(name[0..3], name[3..6], StringComparison.Ordinal)))
                return false;
        }
        symbol = new Symbol(name);
        return true;
    }
}

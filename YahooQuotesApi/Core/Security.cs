using System.Reflection;
using System.Text.Json;

namespace YahooQuotesApi;

#pragma warning disable CA1724 // The type name Security conflicts...
public sealed partial class Security
#pragma warning restore CA1724
{
    private readonly ILogger Logger;
    public Dictionary<string, Prop> Props { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Security(Symbol symbol, ILogger logger)
    {
        Symbol = symbol;
        Logger = logger;
    }
    internal Security(JsonElement jsonElement, ILogger logger)
    {
        Logger = logger;

        foreach (JsonProperty jProperty in jsonElement.EnumerateObject())
            SetProperty(jProperty);

        foreach (var pi in GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!Props.ContainsKey(pi.Name))
                Props.Add(pi.Name, new Prop(pi.Name, pi.IsCalculated() ? PropCategory.Calculated : PropCategory.Missing, null, pi, pi.GetValue(this)));
        }

        if (Currency.Length > 0 && !Symbol.TryCreate(Currency, out Symbol _))
            Logger.LogWarning("Invalid currency symbol: '{Currency}'.", Currency);
    }

    private void SetProperty(JsonProperty jProperty)
    {
        string jName = ReNameProperty(jProperty.Name);

        PropertyInfo? pi = GetType().GetProperty(jName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        if (pi is null)
        {
            Logger.LogTrace("Setting security new property: {Name} = {Value}", jName, jProperty.Value.GetRawText());
            object? val = jProperty.DeserializeNewValue();
            Props.Add(jName, new Prop(jName, PropCategory.New, jProperty, null, val));
            return;
        }

        if (jName == "Symbol")
        {
            string? symbol = jProperty.Value.GetString();
            if (string.IsNullOrEmpty(symbol))
                throw new InvalidOperationException("Empty symbol.");
            if (symbol.EndsWith("=X", StringComparison.OrdinalIgnoreCase) && symbol.Length == 5)
                symbol = "USD" + symbol;
            Logger.LogTrace("Setting security property: Symbol = {Symbol}", symbol);
            Symbol = symbol.ToSymbol();
            Props.Add(jName, new Prop(jName, PropCategory.Expected, jProperty, pi, Symbol));
            return;
        }

        Logger.LogTrace("Setting security property: {Name} = {Value}", jName, jProperty.Value.GetRawText());
        object? value = jProperty.GetValue(pi.PropertyType, Logger);
        if (!pi.CanWrite)
            throw new InvalidOperationException($"Cannot write to property: {jName}.");
        pi.SetValue(this, value);
        Props.Add(jName, new Prop(jName, PropCategory.Expected, jProperty, pi, value));
    }

    private static string ReNameProperty(string name) =>
        name switch
        {
            "regularMarketTime" => "RegularMarketTimeSeconds",
            "preMarketTime" => "PreMarketTimeSeconds",
            "postMarketTime" => "PostMarketTimeSeconds",
            "dividendDate" => "DividendDateSeconds",
            "expireDate" => "ExpireDateSeconds",
            _ => name.ToPascal()
        };
}

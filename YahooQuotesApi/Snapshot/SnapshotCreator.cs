using System.IO;
using System.Reflection;
using System.Text.Json;
namespace YahooQuotesApi;

public sealed class SnapshotCreator(ILogger logger)
{
    private ILogger Logger { get; } = logger;

    internal List<Snapshot> CreateFromJson(JsonDocument jdoc)
    {
        if (!jdoc.RootElement.TryGetProperty("quoteResponse", out JsonElement quoteResponse))
            throw new InvalidDataException("quoteResponse");

        if (!quoteResponse.TryGetProperty("error", out JsonElement error))
            throw new InvalidDataException("error");

        if (error.ValueKind is not JsonValueKind.Null)
        {
            string errorMessage = error.ToString();
            if (error.TryGetProperty("description", out JsonElement property))
            {
                string? description = property.GetString();
                if (description is not null)
                    errorMessage = description;
            }
            throw new InvalidDataException($"Error requesting snapshot: {errorMessage}");
        }

        if (!quoteResponse.TryGetProperty("result", out JsonElement result))
            throw new InvalidDataException("result");

        List<Snapshot> snapshots = new(result.GetArrayLength());
        foreach (JsonElement je in result.EnumerateArray())
            snapshots.Add(CreateFromJson(je));

        return snapshots;
    }

    private Snapshot CreateFromJson(JsonElement je)
    {
        Dictionary<string, object?> properties = new(120, StringComparer.OrdinalIgnoreCase);

        var snapshot = new Snapshot();
        foreach (JsonProperty jp in je.EnumerateObject())
            properties.Add(jp.Name, SetGetProperty(jp, snapshot));

        snapshot.Properties = properties.AsReadOnly();

        return snapshot;
    }

    private object? SetGetProperty(JsonProperty jp, Snapshot snapshot)
    {
        if (jp.Name == "symbol")
        {
            string? symbol = jp.Value.GetString();
            if (string.IsNullOrEmpty(symbol))
                throw new InvalidOperationException("Empty symbol.");
            if (symbol.EndsWith("=X", StringComparison.OrdinalIgnoreCase) && symbol.Length == 5)
                symbol = "USD" + symbol;
            Logger.LogTrace("Setting history property: Symbol = {Symbol}", symbol);
            snapshot.Symbol = symbol.ToSymbol();
            return snapshot.Symbol;
        }
        if (jp.Name == "currency")
        {
            string? currency = jp.Value.GetString();
            if (!string.IsNullOrEmpty(currency))
            {
                if (currency.Length != 3)
                    throw new InvalidOperationException($"Invalid currency: '{currency}'.");
                Logger.LogTrace("Setting history property: Currency = {Currency}", currency);
                snapshot.Currency = $"{currency}=X".ToSymbol();
            }
            return snapshot.Currency;
        }
        if (jp.Name == "firstTradeDateMilliseconds")
        {
            Logger.LogTrace("Setting history property: FirstTradeDate = {FirstTradeDate}", snapshot.FirstTradeDate);
            snapshot.FirstTradeDate = jp.Value.GetInt64().ToInstantFromMilliseconds();
            return snapshot.FirstTradeDate;
        }

        PropertyInfo? pi = typeof(Snapshot).GetProperty(jp.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (pi is not null)
        {
            Logger.LogTrace("Setting history property: {Name} = {Value}", pi.Name, jp.Value.GetRawText());
            if (!pi.CanWrite)
                throw new InvalidOperationException($"Cannot write to property: {pi.Name}.");
            if (jp.Value.GetPrimitive(pi.PropertyType, out object? value))
                pi.SetValue(snapshot, value);
            return value;
        }
        Logger.LogTrace("New history property: {Name} = {Value}", jp.Name, jp.Value.GetRawText());
        return jp.Value.GetPrimitive();
    }
}

using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Text.Json;
namespace YahooQuotesApi;

public sealed class HistoryCreator(IClock clock, ILogger logger)
{
    private IClock Clock { get; } = clock;
    private ILogger Logger { get; } = logger;

    internal Result<History> CreateFromJson(JsonDocument jdoc, string symbol)
    {
        if (!jdoc.TryGetChartProperty("error", out JsonElement error))
            throw new InvalidDataException("No error property.");
        if (error.ValueKind is not JsonValueKind.Null)
        {
            if (error.TryGetProperty("description", out JsonElement property))
            {
                string? description = property.GetString();
                if (description is not null)
                    return Result<History>.Fail($"{symbol}: {description}");
            }
            throw new InvalidDataException($"{symbol}: Unknown error");
        }
        History history = new();
        SetMeta(jdoc, history);
        Result<ImmutableArray<Tick>> result = GetTicksResult(jdoc);
        if (result.HasError)
            return Result<History>.Fail(result.Error);
        history.Ticks = result.Value;
        history.Dividends = GetDividends(jdoc);
        history.Splits = GetSplits(jdoc);
        return history.ToResult();
    }
    internal static History CreateFromSymbol(Symbol symbol) => new() { Symbol = symbol };

    private void SetMeta(JsonDocument jdoc, History history)
    {
        if (!jdoc.TryGetChartProperty("meta", out JsonElement meta))
            throw new ArgumentException("No 'meta' property found.");

        Dictionary<string, object?> properties = new(StringComparer.OrdinalIgnoreCase);

        foreach (JsonProperty jp in meta.EnumerateObject().Where(jp => jp.Name != "validRanges" && jp.Name != "tradingPeriods"))
            properties.Add(jp.Name, SetGetMetaProperty(jp, history));

        history.Properties = properties.AsReadOnly();
    }

    private object? SetGetMetaProperty(JsonProperty jp, History history)
    {
        if (jp.Name == "symbol")
        {
            string? symbol = jp.Value.GetString();
            if (string.IsNullOrEmpty(symbol))
                throw new InvalidOperationException("Empty symbol.");
            if (symbol.EndsWith("=X", StringComparison.OrdinalIgnoreCase) && symbol.Length == 5)
                symbol = "USD" + symbol;
            Logger.LogTrace("Setting history property: Symbol = {Symbol}", symbol);
            history.Symbol = symbol.ToSymbol();
            return history.Symbol;
        }
        if (jp.Name == "currency")
        {
            string? currency = jp.Value.GetString();
            if (!string.IsNullOrEmpty(currency))
            {
                if (currency.Length != 3)
                    throw new InvalidOperationException($"Invalid currency: '{currency}'.");
                Logger.LogTrace("Setting history property: Currency = {Currency}", currency);
                history.Currency = $"{currency}=X".ToSymbol();
            }
            return history.Currency;
        }
        if (jp.Name == "currentTradingPeriod")
        {
            Logger.LogTrace("Setting history property: CurrentTradingPeriod.");
            history.CurrentTradingPeriod = GetCurrentTradingPeriod(jp.Value);
            return history.CurrentTradingPeriod;
        }
        PropertyInfo? pi = typeof(History).GetProperty(jp.Name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (pi is not null)
        {
            Logger.LogTrace("Setting history property: {Name} = {Value}", pi.Name, jp.Value.GetRawText());
            if (!pi.CanWrite)
                throw new InvalidOperationException($"Cannot write to property: {pi.Name}.");
            if (jp.Value.GetPrimitive(pi.PropertyType, out object? value))
                pi.SetValue(history, value);
            return value;
        }
        Logger.LogTrace("New history property: {Name} = {Value}", jp.Name, jp.Value.GetRawText());
        return jp.Value.GetPrimitive();
    }

    private static Result<ImmutableArray<Tick>> GetTicksResult(JsonDocument jdoc)
    {
        if (!jdoc.TryGetChartProperty("timestamp", out JsonElement timestampArray))
            return Result<ImmutableArray<Tick>>.Fail("No 'timestamp' property found.");

        int length = timestampArray.GetArrayLength();
        var builder = ImmutableArray.CreateBuilder<Tick>(length);
        JsonElement openArray = jdoc.GetJsonArray("open", length);
        JsonElement highArray = jdoc.GetJsonArray("high", length);
        JsonElement lowArray = jdoc.GetJsonArray("low", length);
        JsonElement closeArray = jdoc.GetJsonArray("close", length);
        JsonElement adjcloseArray = jdoc.GetJsonArray("adjclose", length);
        JsonElement volumeArray = jdoc.GetJsonArray("volume", length);
        Instant previous = Instant.MinValue;
        for (int i = 0; i < length; i++)
        {
            var date = timestampArray.GetArrayElementAtIndex<Int64>(i).ToInstantFromSeconds();
            if (date <= previous)
                return Result<ImmutableArray<Tick>>.Fail("Timestamps are not in order.");
            previous = date;
            builder.Add(new(
                date,
                openArray.GetArrayElementAtIndex<Double>(i),
                highArray.GetArrayElementAtIndex<Double>(i),
                lowArray.GetArrayElementAtIndex<Double>(i),
                closeArray.GetArrayElementAtIndex<Double>(i),
                adjcloseArray.GetArrayElementAtIndex<Double>(i),
                volumeArray.GetArrayElementAtIndex<Int64>(i)));
        }
        return builder.MoveToImmutable().ToResult();
    }

    private static ImmutableArray<Dividend> GetDividends(JsonDocument jdoc)
    {
        if (!jdoc.TryGetChartProperty("dividends", out JsonElement dividendsObject))
            return [];
        int length = dividendsObject.EnumerateObject().Count();
        var builder = ImmutableArray.CreateBuilder<Dividend>(length);
        foreach (JsonProperty jp in dividendsObject.EnumerateObject())
            builder.Add(new Dividend(jp.Value.GetProperty("date").GetInt64().ToInstantFromSeconds(), jp.Value.GetProperty("amount").GetDecimal()));
        return builder.MoveToImmutable();
    }

    private static ImmutableArray<Split> GetSplits(JsonDocument jdoc)
    {
        if (!jdoc.TryGetChartProperty("splits", out JsonElement splitsObject))
            return [];
        var length = splitsObject.EnumerateObject().Count();
        var builder = ImmutableArray.CreateBuilder<Split>(length);
        foreach (JsonProperty jp in splitsObject.EnumerateObject())
            builder.Add(new Split(jp.Value.GetProperty("date").GetInt64().ToInstantFromSeconds(), jp.Value.GetProperty("numerator").GetDecimal(), jp.Value.GetProperty("denominator").GetDecimal()));
        return builder.MoveToImmutable();
    }

    private static ImmutableArray<TradingPeriod> GetCurrentTradingPeriod(JsonElement currentTradingPeriodObject)
    {
        int length = currentTradingPeriodObject.EnumerateObject().Count();
        var builder = ImmutableArray.CreateBuilder<TradingPeriod>(length);
        foreach (JsonProperty jp in currentTradingPeriodObject.EnumerateObject())
        {
            string name = jp.Name;
            JsonElement je = jp.Value;
            builder.Add(new TradingPeriod(
                name,
                je.GetProperty("start").GetInt64().ToInstantFromSeconds(),
                je.GetProperty("end").GetInt64().ToInstantFromSeconds(),
                je.GetProperty("gmtoffset").GetInt32(),
                je.GetProperty("timezone").GetString() ?? ""));
        }
        return builder.MoveToImmutable();
    }
}

static file class HistoryExtensions
{
    internal static bool TryGetChartProperty(this JsonDocument jdoc, string name, out JsonElement jeOut)
    {
        string[] path = name switch
        {
            "error" => ["chart", "error"],
            "timestamp" => ["chart", "result", "timestamp"],
            "open" => ["chart", "result", "indicators", "quote", "open"],
            "high" => ["chart", "result", "indicators", "quote", "high"],
            "low" => ["chart", "result", "indicators", "quote", "low"],
            "close" => ["chart", "result", "indicators", "quote", "close"],
            "volume" => ["chart", "result", "indicators", "quote", "volume"],
            "adjclose" => ["chart", "result", "indicators", "adjclose", "adjclose"],
            "dividends" => ["chart", "result", "events", "dividends"],
            "splits" => ["chart", "result", "events", "splits"],
            "meta" => ["chart", "result", "meta"],
            _ => throw new ArgumentException($"Invalid property name: '{name}'.")
        };
        jeOut = default; // ValueKind.Undefined;
        JsonElement je = jdoc.RootElement;
        foreach (string p in path)
        {
            if (je.ValueKind == JsonValueKind.Object)
            {
                if (!je.TryGetProperty(p, out je))
                    return false;
                continue;
            }
            if (je.ValueKind == JsonValueKind.Array)
            {
                JsonElement jex = default;
                foreach (JsonElement x in je.EnumerateArray())
                {
                    if (x.TryGetProperty(p, out jex))
                        break;
                }
                if (jex.ValueKind == JsonValueKind.Undefined)
                    return false;
                je = jex;
                continue;
            }
        }
        jeOut = je;
        return true;
    }

    internal static JsonElement GetJsonArray(this JsonDocument jdoc, string name, int length)
    {
        if (jdoc.TryGetChartProperty(name, out JsonElement ja))
        {
            if (ja.GetArrayLength() != length)
                throw new InvalidDataException($"Mismatched array size: {name}.");
        }
        return ja; // undefined if not found
    }

    internal static T GetArrayElementAtIndex<T>(this JsonElement ja, int index, T defaultValue = default) where T : struct
    {
        if (ja.ValueKind == JsonValueKind.Undefined) // empty array
            return defaultValue;
        if (ja.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Not array ValueKind: '{ja.ValueKind}'.");
        JsonElement je = ja[index];
        if (je.GetPrimitive(typeof(T), out object? val) && val != null)
            return (T)val;
        return defaultValue;
    }
}

using System.Text.Json;
namespace YahooQuotesApi;

internal static class JsonExtensions
{
    extension(JsonElement je)
    {
        internal object? GetPrimitive()
        {
            return je.ValueKind switch
            {
                JsonValueKind.Undefined => null,
                JsonValueKind.Null => null,
                JsonValueKind.String => je.GetString() ?? "",
                JsonValueKind.True => je.GetBoolean(),
                JsonValueKind.False => je.GetBoolean(),
                JsonValueKind.Number => je.GetDouble(),
                JsonValueKind.Object => je.Clone(),
                JsonValueKind.Array => je.Clone(),
                _ => throw new NotImplementedException($"Unhandled type: {je.ValueKind}.")
            };
        }

        internal bool GetPrimitive(Type type, out object? value)
        {
            if (je.ValueKind == JsonValueKind.Object || je.ValueKind == JsonValueKind.Array)
                ThrowError();

            Type? underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType is not null)
            {
                if (je.ValueKind == JsonValueKind.Null)
                {
                    value = null;
                    return true;
                }
                type = underlyingType;
            }

            if (je.ValueKind == JsonValueKind.Null || je.ValueKind == JsonValueKind.Undefined)
            {
                value = null;
                return false;
            }

            if (type == typeof(string))
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    value = je.GetString() ?? "";
                    return true;
                }
                ThrowError();
            }
            if (type == typeof(bool))
            {
                if (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False)
                {
                    value = je.GetBoolean();
                    return true;
                }
                ThrowError();
            }
            if (je.ValueKind != JsonValueKind.Number)
                ThrowError();

            value = type switch
            {
                Type t when t == typeof(int) => je.GetInt32(),
                Type t when t == typeof(long) => je.GetInt64(),
                Type t when t == typeof(float) => je.GetSingle(),
                Type t when t == typeof(double) => je.GetDouble(),
                Type t when t == typeof(decimal) => je.GetDecimal(),
                Type t when t == typeof(Instant) => je.GetInt64().ToInstantFromSeconds(),
                _ => throw new NotImplementedException($"Unhandled type: {type}.")
            };

            return true;

            void ThrowError() => throw new NotImplementedException($"Unhandled conversion: '{je.ValueKind} => {type}.");
        }
    }
}

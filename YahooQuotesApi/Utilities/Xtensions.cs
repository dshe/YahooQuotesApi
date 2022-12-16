using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;

namespace YahooQuotesApi;

internal static partial class Xtensions
{
    internal static string ToPascal(this string source)
    {
        if (source.Length == 0)
            return source;
        char[] chars = source.ToCharArray();
        chars[0] = char.ToUpper(chars[0], CultureInfo.InvariantCulture);
        return new string(chars);
    }

    internal static string Name<T>(this T source) where T : Enum
    {
        string name = source.ToString();
        if (typeof(T).GetMember(name).First().GetCustomAttribute(typeof(EnumMemberAttribute)) is EnumMemberAttribute attr
            && attr.IsValueSetExplicitly && attr.Value is not null)
            name = attr.Value;
        return name;
    }

    internal static IEnumerable<T> NotNull<T>(this IEnumerable<T?> source) where T : class
    {
        foreach (T? item in source)
        {
            if (item is not null)
                yield return item;
        }
    }

    internal static double RoundToSigFigs(this double num, int figs)
    {
        if (num == 0)
            return 0;

        double d = Math.Ceiling(Math.Log10(num < 0 ? -num : num));
        int power = figs - (int)d;

        double magnitude = Math.Pow(10, power);
        double shifted = Math.Round(num * magnitude);
        return shifted / magnitude;
    }

    internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> items) => new(items);

    internal static object? GetJsonPropertyValueOfType(this JsonProperty jproperty, Type propertyType)
    {
        JsonElement value = jproperty.Value;
        JsonValueKind kind = value.ValueKind;

        if (kind == JsonValueKind.String)
            return value.GetString();

        if (kind == JsonValueKind.True || kind == JsonValueKind.False)
            return value.GetBoolean();

        if (kind == JsonValueKind.Number)
        {
            if (propertyType == typeof(Int64) || propertyType == typeof(Int64?))
                return value.GetInt64();
            if (propertyType == typeof(Double) || propertyType == typeof(Double?))
                return value.GetDouble();
            if (propertyType == typeof(Decimal) || propertyType == typeof(Decimal?))
                return value.GetDecimal();
        }

        throw new InvalidDataException($"Unsupported type: {propertyType} for property: {jproperty.Name}.");
    }

    internal static object? GetJsonPropertyValue(this JsonProperty jproperty)
    {
        JsonElement value = jproperty.Value;
        JsonValueKind kind = value.ValueKind;

        if (kind == JsonValueKind.String)
            return value.GetString(); // may return null

        if (kind == JsonValueKind.True || kind == JsonValueKind.False)
            return value.GetBoolean();

        if (kind == JsonValueKind.Number)
        {
            if (value.TryGetInt64(out long l))
                return l;
            if (value.TryGetDouble(out double dbl))
                return dbl;
        }
        return value.GetRawText();
    }

    internal static ReadOnlySpan<char> SplitNext(this ref ReadOnlySpan<char> span, char separator)
    {
        int pos = span.IndexOf(separator);
        if (pos > -1)
        {
            ReadOnlySpan<char> part = span[..pos];
            span = span[(pos + 1)..];
            return part;
        }
        ReadOnlySpan<char> lastPart = span;
        span = span[span.Length..];
        return lastPart;
    }

}

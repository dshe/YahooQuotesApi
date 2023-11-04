using System.Reflection;
using System.Text.Json;

namespace YahooQuotesApi;

/*
CATEGORY     JSON   PROPERTY    VALUE
expected       Y       Y     deserialized
calculated     N       Y      calculated
missing        N       Y       default
new            Y       N     deserialized
*/

public enum PropCategory
{
    New, Missing, Expected, Calculated
}

public sealed record class Prop(string Name, PropCategory Category, JsonProperty? JProperty, PropertyInfo? PropertyInfo, object? Value);

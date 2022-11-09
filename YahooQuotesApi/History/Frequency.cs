using System.Runtime.Serialization;

namespace YahooQuotesApi;

public enum Frequency
{
    [EnumMember(Value = "d")]
    Daily,

    [EnumMember(Value = "wk")]
    Weekly,

    [EnumMember(Value = "mo")]
    Monthly
}

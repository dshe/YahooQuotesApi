namespace YahooQuotesApi;

public static class DateTimeExtensions
{
    internal static Instant ToInstantFromSeconds(this long unixTimeSeconds) => Instant.FromUnixTimeSeconds(unixTimeSeconds);
    internal static Instant ToInstantFromMilliseconds(this long unixTimeMilliseconds) => Instant.FromUnixTimeMilliseconds(unixTimeMilliseconds);
}

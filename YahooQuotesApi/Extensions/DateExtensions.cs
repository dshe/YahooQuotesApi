namespace YahooQuotesApi;

internal static class DateTimeExtensions
{
    extension(long unixTime)
    {
        internal Instant ToInstantFromSeconds() => Instant.FromUnixTimeSeconds(unixTime);
        internal Instant ToInstantFromMilliseconds() => Instant.FromUnixTimeMilliseconds(unixTime);
    }
}

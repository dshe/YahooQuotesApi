namespace YahooQuotesApi;

public static class Helpers
{
    private static readonly DateTimeZone SystemTimeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault();

    public static DateTimeZone GetTimeZone(string timeZoneName)
    {
        DateTimeZone? tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneName);
        return tz is not null ? tz : throw new InvalidTimeZoneException($"Invalid timeZone: '{timeZoneName}'.");
    }

    // Default timeZone is system, else use "UTC", "America/New_York"...
    public static ZonedDateTime UnixSecondsToDateTime(long unixSeconds, string timeZoneName = "")
    {
        if (unixSeconds <= 0)
            throw new InvalidOperationException("Invalid unixSeconds.");
        DateTimeZone tz = string.IsNullOrWhiteSpace(timeZoneName) ? SystemTimeZone : GetTimeZone(timeZoneName);
        return Instant.FromUnixTimeSeconds(unixSeconds).InZone(tz);
    }

    // Default timeZone is system, else use "UTC", "America/New_York"...
    public static ZonedDateTime UnixMillisecondsToDateTime(long unixMilliseconds, string timeZoneName = "")
    {
        if (unixMilliseconds <= 0)
            throw new InvalidOperationException("Invalid unixMilliseconds.");
        DateTimeZone tz = string.IsNullOrWhiteSpace(timeZoneName) ? SystemTimeZone : GetTimeZone(timeZoneName);
        return Instant.FromUnixTimeMilliseconds(unixMilliseconds).InZone(tz);
    }

    public static LocalTime GetExchangeCloseTimeFromSymbol(Symbol symbol) => Exchanges.GetCloseTimeFromSymbol(symbol);
}

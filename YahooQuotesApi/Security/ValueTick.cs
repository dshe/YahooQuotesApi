namespace YahooQuotesApi;

public sealed record class ValueTick(Instant Date, double Value, long Volume);

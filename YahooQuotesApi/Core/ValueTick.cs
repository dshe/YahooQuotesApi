namespace YahooQuotesApi;

// 12 + 8 + 8 = 28 bytes
// Record classes immutable by default, record structs are mutable
// This is to be consistent with tuples. Tuples are like anonymous record structs with similar features.
// Struct mutability does not carry the same level of concern as class mutability.

public sealed record class ValueTick(Instant Date, double Value, long Volume);

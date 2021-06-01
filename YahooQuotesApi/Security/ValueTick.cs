using NodaTime;

namespace YahooQuotesApi
{
    public sealed class ValueTick
    {
        public Instant Date { get; init; }
        public double Value { get; init; }
        public long Volume { get; init; } = 0;

        public override string ToString() => $"{Date}, {Value}, {Volume}";
    }
}

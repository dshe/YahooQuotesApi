namespace YahooQuotesApi;

public sealed class SplitTick : ITick
{
    public LocalDate Date { get; init; }
    public double BeforeSplit { get; init; }
    public double AfterSplit { get; init; }

    public override string ToString() => $"{Date} {BeforeSplit}:{AfterSplit}";
}

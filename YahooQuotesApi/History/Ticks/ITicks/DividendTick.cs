namespace YahooQuotesApi;

public sealed class DividendTick : ITick
{
    // ex-dividend date
    public LocalDate Date { get; init; } 
    public double Dividend { get; init; }

    public override string ToString() => $"{Date}, {Dividend}";
}

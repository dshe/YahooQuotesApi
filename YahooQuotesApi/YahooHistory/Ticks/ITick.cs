using NodaTime;

#nullable enable

namespace YahooQuotesApi
{
    public interface ITick
    {
        LocalDate Date { get; }
    }
}

using NodaTime;

namespace YahooQuotesApi
{
    public interface ITick
    {
        LocalDate Date { get; }
    }
}

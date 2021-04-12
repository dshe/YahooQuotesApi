using NodaTime;

namespace YahooQuotesApi
{
    interface ITick
    {
        public LocalDate Date { get; }
    }
}


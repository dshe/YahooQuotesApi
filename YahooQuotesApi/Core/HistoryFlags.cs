using System;

namespace YahooQuotesApi
{
    [Flags]
    public enum HistoryFlags
    {
        None = 0,
        PriceHistory = 1,
        DividendHistory = 2,
        SplitHistory = 4,
        All = ~0
    }
}

using System;

namespace YahooQuotesApi
{
    [Flags]
    internal enum HistoryFlags
    {
        None = 0,
        PriceHistory = 1,
        DividendHistory = 2,
        SplitHistory = 4,
        All = ~0
    }
}

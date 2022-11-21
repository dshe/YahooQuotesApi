using System;

namespace YahooQuotesApi;

// A1717: Only FlagsAttribute enums should have plural names

[Flags]
public enum Histories
{
    None = 0,
    PriceHistory = 1,
    DividendHistory = 2,
    SplitHistory = 4,
    All = 7
}

using System.Collections.ObjectModel;
namespace YahooQuotesApi;

public sealed class Snapshot
{
    public Symbol Symbol { get; internal set; }
    public string UnderlyingSymbol { get; internal set; } = "";
    public string UnderlyingExchangeSymbol { get; internal set; } = "";
    public string HeadSymbolAsString { get; internal set; } = "";
    public bool? ContractSymbol { get; internal set; }
    public Symbol Currency { get; internal set; }
    public string FinancialCurrency { get; internal set; } = "";
    public string LongName { get; internal set; } = "";
    public string ShortName { get; internal set; } = "";
    public string DisplayName { get; internal set; } = "";
    public string Language { get; internal set; } = "";
    public string Region { get; internal set; } = "";
    public string QuoteType { get; internal set; } = "";
    public string TypeDisp { get; internal set; } = "";
    public string QuoteSourceName { get; internal set; } = "";
    public string MessageBoardId { get; internal set; } = "";

    public bool? Tradeable { get; internal set; }
    public bool? CryptoTradeable { get; internal set; }
    public bool? Triggerable { get; internal set; }
    public bool? EsgPopulated { get; internal set; }
    
    public long? PriceHint { get; internal set; }
    public long? SourceInterval { get; internal set; }
    public Instant FirstTradeDate { get; internal set; }

    public string Market { get; internal set; } = "";
    public string MarketState { get; internal set; } = "";
    public double MarketCap { get; internal set; }
    public long SharesOutstanding { get; internal set; }
    public long OpenInterest { get; internal set; }

    public decimal Bid { get; internal set; }
    public long BidSize { get; internal set; }
    public decimal Ask { get; internal set; }
    public long AskSize { get; internal set; }

    public decimal RegularMarketPreviousClose { get; internal set; }
    public decimal RegularMarketOpen { get; internal set; }
    public decimal RegularMarketDayHigh { get; internal set; }
    public decimal RegularMarketDayLow { get; internal set; }
    public string RegularMarketDayRange { get; internal set; } = "";
    public long RegularMarketVolume { get; internal set; }
    public long AverageDailyVolume10Day { get; internal set; }
    public long AverageDailyVolume3Month { get; internal set; }

    public Instant RegularMarketTime { get; internal set; }
    public decimal RegularMarketPrice { get; internal set; }
    public decimal RegularMarketChange { get; internal set; }
    public double RegularMarketChangePercent { get; internal set; }

    public Instant PreMarketTime { get; internal set; }
    public decimal PreMarketPrice { get; internal set; }
    public decimal PreMarketChange { get; internal set; }
    public double PreMarketChangePercent { get; internal set; }

    public Instant PostMarketTime { get; internal set; }
    public decimal PostMarketPrice { get; internal set; }
    public decimal PostMarketChange { get; internal set; }
    public double PostMarketChangePercent { get; internal set; }

    public bool HasPrePostMarketData { get; internal set; }
    public string Exchange { get; internal set; } = "";
    public string FullExchangeName { get; internal set; } = "";
    public long ExchangeDataDelayedBy { get; internal set; } = long.MinValue;
    public long GmtOffSetMilliseconds { get; internal set; } = long.MinValue;
    public string ExchangeTimezoneName { get; internal set; } = "";
    public string ExchangeTimezoneShortName { get; internal set; } = "";

    public Instant DividendDate { get; internal set; }
    public decimal DividendRate { get; internal set; }
    public decimal TrailingAnnualDividendRate { get; internal set; }
    public double DividendYield { get; internal set; }
    public double TrailingAnnualDividendYield { get; internal set; }
    public double PriceEpsCurrentYear { get; internal set; }
    public decimal EpsCurrentYear { get; internal set; }
    public decimal EpsForward { get; internal set; }
    public decimal EpsTrailingTwelveMonths { get; internal set; }

    public Instant EarningsTimestamp { get; internal set; }
    public Instant EarningsTimestampStart { get; internal set; }
    public Instant EarningsTimestampEnd { get; internal set; }
    public Instant EarningsCallTimestampStart { get; internal set; }
    public Instant EarningsCallTimestampEnd { get; internal set; }
    public bool IsEarningsDateEstimate { get; internal set; }

    public string ExpireIsoDate { get; internal set; } = "";
    public Instant ExpireDate { get; internal set; }

    public decimal BookValue { get; internal set; }
    public double NetAssets { get; internal set; }
    public double TrailingPE { get; internal set; }
    public double ForwardPE { get; internal set; }
    public double PriceToBook { get; internal set; }
    public double NetExpenseRatio { get; internal set; }
    public double TrailingThreeMonthNavReturns { get; internal set; }
    public double TrailingThreeMonthReturns { get; internal set; }
    public double YtdReturn { get; internal set; }
    public double FiftyDayAverage { get; internal set; }
    public double FiftyDayAverageChange { get; internal set; }
    public double FiftyDayAverageChangePercent { get; internal set; }
    public double TwoHundredDayAverage { get; internal set; }
    public double TwoHundredDayAverageChange { get; internal set; }
    public double TwoHundredDayAverageChangePercent { get; internal set; }
    public double FiftyTwoWeekChangePercent { get; internal set; }
    public decimal FiftyTwoWeekHigh { get; internal set; }
    public double FiftyTwoWeekHighChange { get; internal set; }
    public double FiftyTwoWeekHighChangePercent { get; internal set; }
    public decimal FiftyTwoWeekLow { get; internal set; }
    public double FiftyTwoWeekLowChange { get; internal set; }
    public double FiftyTwoWeekLowChangePercent { get; internal set; }
    public string FiftyTwoWeekRange { get; internal set; } = "";
    public string AverageAnalystRating { get; internal set; } = "";
    public string CustomPriceAlertConfidence { get; internal set; } = "";
    public IReadOnlyDictionary<string, object?> Properties { get; internal set; } = ReadOnlyDictionary<string, object?>.Empty;
}

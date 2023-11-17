namespace YahooQuotesApi;

#pragma warning disable CA1724 // The type name Security conflicts...
public sealed partial class Security
#pragma warning restore CA1724
{
    public Dictionary<string, Prop> Props { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Symbol Symbol { get; private set; }
    public string UnderlyingSymbol { get; private set; } = "";
    public string UnderlyingExchangeSymbol { get; private set; } = "";
    public string HeadSymbolAsString { get; private set; } = "";
    public string Currency { get; private set; } = "";
    public string FinancialCurrency { get; private set; } = "";
    public string LongName { get; private set; } = "";
    public string ShortName { get; private set; } = "";
    public string Language { get; private set; } = "";
    public string Region { get; private set; } = "";
    public string QuoteType { get; private set; } = "";
    public string QuoteSourceName { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string TypeDisp { get; private set; } = "";
    public string MessageBoardId { get; private set; } = "";

    public bool? ContractSymbol { get; private set; }
    public bool? Tradeable { get; private set; }
    public bool? CryptoTradeable { get; private set; }
    public bool? Triggerable { get; private set; }
    public bool? EsgPopulated { get; private set; }
    
    public long? PriceHint { get; private set; }
    public long? SourceInterval { get; private set; }
    public long FirstTradeDateMilliseconds { get; private set; }

    public string Market { get; private set; } = "";
    public string MarketState { get; private set; } = "";
    public double? MarketCap { get; private set; }
    public long? SharesOutstanding { get; private set; }
    public long? OpenInterest { get; private set; }

    public decimal? Bid { get; private set; }
    public long? BidSize { get; private set; }
    public decimal? Ask { get; private set; }
    public long? AskSize { get; private set; }

    public decimal? RegularMarketPreviousClose { get; private set; }
    public decimal? RegularMarketOpen { get; private set; }
    public decimal? RegularMarketDayHigh { get; private set; }
    public decimal? RegularMarketDayLow { get; private set; }
    public string RegularMarketDayRange { get; private set; } = "";
    public long? RegularMarketVolume { get; private set; }
    public long? AverageDailyVolume10Day { get; private set; }
    public long? AverageDailyVolume3Month { get; private set; }

    public long RegularMarketTimeSeconds { get; private set; }
    public decimal? RegularMarketPrice { get; private set; }
    public decimal? RegularMarketChange { get; private set; }
    public double? RegularMarketChangePercent { get; private set; }

    public long PreMarketTimeSeconds { get; private set; }
    public decimal? PreMarketPrice { get; private set; }
    public decimal? PreMarketChange { get; private set; }
    public double? PreMarketChangePercent { get; private set; }

    public long PostMarketTimeSeconds { get; private set; }
    public decimal? PostMarketPrice { get; private set; }
    public decimal? PostMarketChange { get; private set; }
    public double? PostMarketChangePercent { get; private set; }

    public string Exchange { get; private set; } = "";
    public string FullExchangeName { get; private set; } = "";
    public long? ExchangeDataDelayedBy { get; private set; }
    public long? GmtOffSetMilliseconds { get; private set; }
    public string ExchangeTimezoneName { get; private set; } = "";
    public string ExchangeTimezoneShortName { get; private set; } = "";

    public long DividendDateSeconds { get; private set; }
    public decimal? DividendRate { get; private set; }
    public decimal? TrailingAnnualDividendRate { get; private set; }
    public double? DividendYield { get; private set; }
    public double? TrailingAnnualDividendYield { get; private set; }
    public double? PriceEpsCurrentYear { get; private set; }
    public decimal? EpsCurrentYear { get; private set; }
    public decimal? EpsForward { get; private set; }
    public decimal? EpsTrailingTwelveMonths { get; private set; }

    public long EarningsTimestamp { get; private set; }
    public long EarningsTimestampStart { get; private set; }
    public long EarningsTimestampEnd { get; private set; }

    public string ExpireIsoDate { get; private set; } = "";
    public long ExpireDateSeconds { get; private set; }

    public decimal? BookValue { get; private set; }
    public double? NetAssets { get; private set; }
    public double? TrailingPE { get; private set; }
    public double? ForwardPE { get; private set; }
    public double? PriceToBook { get; private set; }
    public double? NetExpenseRatio { get; private set; }
    public double? TrailingThreeMonthNavReturns { get; private set; }
    public double? TrailingThreeMonthReturns { get; private set; }
    public double? YtdReturn { get; private set; }
    public double? FiftyDayAverage { get; private set; }
    public double? FiftyDayAverageChange { get; private set; }
    public double? FiftyDayAverageChangePercent { get; private set; }
    public double? TwoHundredDayAverage { get; private set; }
    public double? TwoHundredDayAverageChange { get; private set; }
    public double? TwoHundredDayAverageChangePercent { get; private set; }
    public double? FiftyTwoWeekChangePercent { get; private set; }
    public decimal? FiftyTwoWeekHigh { get; private set; }
    public double? FiftyTwoWeekHighChange { get; private set; }
    public double? FiftyTwoWeekHighChangePercent { get; private set; }
    public decimal? FiftyTwoWeekLow { get; private set; }
    public double? FiftyTwoWeekLowChange { get; private set; }
    public double? FiftyTwoWeekLowChangePercent { get; private set; }
    public string FiftyTwoWeekRange { get; private set; } = "";
    public string AverageAnalystRating { get; private set; } = "";
    public string CustomPriceAlertConfidence { get; private set; } = ""; // testing
    public Result<PriceTick[]> PriceHistory { get; internal set; }
    public Result<ValueTick[]> PriceHistoryBase { get; internal set; }
    public Result<DividendTick[]> DividendHistory { get; internal set; }
    public Result<SplitTick[]> SplitHistory { get; internal set; }
}

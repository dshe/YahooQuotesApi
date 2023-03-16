using System.Reflection;
using System.Text.Json;

namespace YahooQuotesApi;

#pragma warning disable CA1724 // The type name Security conflicts...
public sealed class Security
#pragma warning restore CA1724
{
    private static readonly IDateTimeZoneProvider DateTimeZoneProvider = DateTimeZoneProviders.Tzdb;
    private readonly ILogger Logger;

    internal Security(Symbol symbol, ILogger logger)
    {
        Logger = logger;
        Symbol = symbol;
    }

    internal Security(JsonElement jsonElement, ILogger logger)
    {
        Logger = logger;
        
        foreach (JsonProperty property in jsonElement.EnumerateObject())
            SetProperty(property);

        if (Currency.Length > 0)
        {
            if (!Symbol.TryCreate(Currency, out Symbol _))
                logger.LogWarning("Invalid currency symbol: '{Currency}'.", Currency);
        }

        if (DividendDateSeconds > 0)
            DividendDate = Instant.FromUnixTimeSeconds(DividendDateSeconds).InUtc().LocalDateTime;
        if (EarningsTimestamp > 0)
            EarningsTime = Instant.FromUnixTimeSeconds(EarningsTimestamp).InUtc().LocalDateTime;
        if (EarningsTimestampStart > 0)
            EarningsTimeStart = Instant.FromUnixTimeSeconds(EarningsTimestampStart).InUtc().LocalDateTime;
        if (EarningsTimestampEnd > 0)
            EarningsTimeStart = Instant.FromUnixTimeSeconds(EarningsTimestampEnd).InUtc().LocalDateTime;

        ExchangeCloseTime = Exchanges.GetCloseTimeFromSymbol(Symbol);

        if (ExchangeTimezoneName.Length > 0)
        {
            ExchangeTimezone = DateTimeZoneProvider.GetZoneOrNull(ExchangeTimezoneName);
            if (ExchangeTimezone is null)
                logger.LogWarning("ExchangeTimezone not found for: '{ExchangeTimezoneName}'.", ExchangeTimezoneName);
        }

        if (RegularMarketTimeSeconds > 0 && ExchangeTimezone is not null)
            RegularMarketTime = Instant.FromUnixTimeSeconds(RegularMarketTimeSeconds).InZone(ExchangeTimezone);
        if (PreMarketTimeSeconds > 0 && ExchangeTimezone is not null)
            PreMarketTime = Instant.FromUnixTimeSeconds(PreMarketTimeSeconds).InZone(ExchangeTimezone);
        if (PostMarketTimeSeconds > 0 && ExchangeTimezone is not null)
            PostMarketTime = Instant.FromUnixTimeSeconds(PostMarketTimeSeconds).InZone(ExchangeTimezone);
        if (FirstTradeDateMilliseconds > 0)
            FirstTradeDate = Instant.FromUnixTimeMilliseconds(FirstTradeDateMilliseconds).InUtc().LocalDateTime;
    }

    private void SetProperty(JsonProperty property)
    {
        string jName = property.Name switch
        {
            "regularMarketTime" => "RegularMarketTimeSeconds",
            "preMarketTime" => "PreMarketTimeSeconds",
            "postMarketTime" => "PostMarketTimeSeconds",
            "dividendDate" => "DividendDateSeconds",
            _ => property.Name.ToPascal()
        };

        PropertyInfo? propertyInfo = GetType().GetProperty(jName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (propertyInfo is not null)
        {
            object value = property.GetJsonPropertyValueOfType(propertyInfo.PropertyType) ?? throw new InvalidOperationException("GetJsonPropertyValueOfType");
            if (propertyInfo.Name == "Symbol")
            {
                string symbol = (string)value;
                if (symbol.EndsWith("=X", StringComparison.OrdinalIgnoreCase) && symbol.Length == 5)
                    symbol = "USD" + symbol;
                Logger.LogTrace("Setting security property: Symbol = {Symbol}", symbol);
                Symbol = symbol.ToSymbol();
                return;
            }
            Logger.LogTrace("Setting security property: {Name} = {Value}", propertyInfo.Name, value);
            if (!propertyInfo.CanWrite)
                throw new InvalidOperationException($"Cannot write property {jName}.");
            propertyInfo.SetValue(this, value);
            Properties.Add(jName, value);
            return;
        }

        object? val = property.GetJsonPropertyValue();
        Logger.LogTrace("Setting security other property: {Name} = {Value}", jName, val);
        Properties.Add(jName, val);
    }

    public Dictionary<string, object?> Properties { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public Decimal Ask { get; private set; }
    public Int64 AskSize { get; private set; }
    public String AverageAnalystRating { get; private set; } = "";
    public Int64 AverageDailyVolume10Day { get; private set; }
    public Int64 AverageDailyVolume3Month { get; private set; }
    public Decimal Bid { get; internal set; }
    public Int64 BidSize { get; private set; }
    public Decimal BookValue { get; private set; }
    public Boolean? CryptoTradable { get; private set; }
    public String Currency { get; internal set; } = "";
    public String CustomPriceAlertConfidence { get; private set; } = "";
    public String DisplayName { get; private set; } = "";
    public LocalDateTime DividendDate { get; private set; }
    public Int64 DividendDateSeconds { get; private set; }
    public Result<DividendTick[]> DividendHistory { get; internal set; }
    public LocalDateTime EarningsTime { get; private set; }
    public LocalDateTime EarningsTimeEnd { get; private set; }
    public Int64 EarningsTimestamp { get; private set; }
    public Int64 EarningsTimestampEnd { get; private set; }
    public Int64 EarningsTimestampStart { get; private set; }
    public LocalDateTime EarningsTimeStart { get; private set; }
    public Decimal? EpsCurrentYear { get; private set;  }
    public Decimal? EpsForward { get; private set; }
    public Decimal? EpsTrailingTwelveMonths { get; private set; }
    public Boolean? EsgPopulated { get; private set; }
    public String Exchange { get; private set; } = "";
    public LocalTime ExchangeCloseTime { get; private set; }
    public Int64? ExchangeDataDelayedBy { get; private set; }
    public DateTimeZone? ExchangeTimezone { get; private set; }
    public String ExchangeTimezoneName { get; private set; } = "";
    public String ExchangeTimezoneShortName { get; private set; } = "";
    public Decimal? FiftyDayAverage { get; private set; }
    public Decimal? FiftyDayAverageChange { get; private set; }
    public Decimal? FiftyDayAverageChangePercent { get; private set; }
    public Decimal? FiftyTwoWeekHigh { get; private set; }
    public Decimal? FiftyTwoWeekHighChange { get; private set; }
    public Decimal? FiftyTwoWeekHighChangePercent { get; private set; }
    public Decimal? FiftyTwoWeekLow { get; private set; }
    public Decimal? FiftyTwoWeekLowChange { get; private set; }
    public Decimal? FiftyTwoWeekLowChangePercent { get; private set; }
    public String FiftyTwoWeekRange { get; private set; } = "";
    public String FinancialCurrency { get; private set; } = "";
    public LocalDateTime FirstTradeDate { get; private set; }
    public Int64 FirstTradeDateMilliseconds { get; private set; }
    public Decimal? ForwardPE { get; private set; }
    public String FullExchangeName { get; private set; } = "";
    public Int64? GmtOffSetMilliseconds { get; private set; }
    public String Language { get; private set; } = "";
    public String LongName { get; private set; } = "";
    public String Market { get; private set; } = "";
    public Int64? MarketCap { get; private set; }
    public String MarketState { get; private set; } = "";
    public String MessageBoardId { get; private set; } = "";
    public Decimal? PostMarketChange { get; private set; }
    public Decimal? PostMarketChangePercent { get; private set; }
    public Decimal? PostMarketPrice { get; private set; }
    public ZonedDateTime PostMarketTime { get; private set; }
    public Int64 PostMarketTimeSeconds { get; private set; }
    public Decimal? PreMarketChange { get; private set; }
    public Decimal? PreMarketChangePercent { get; private set; }
    public Decimal? PreMarketPrice { get; private set; }
    public ZonedDateTime PreMarketTime { get; private set; }
    public Int64 PreMarketTimeSeconds { get; private set; }
    public Decimal? PriceEpsCurrentYear { get; private set; }
    public Int64? PriceHint { get; private set; }
    public Result<PriceTick[]> PriceHistory { get; internal set; }
    public Result<ValueTick[]> PriceHistoryBase { get; internal set; }
    public Decimal? PriceToBook { get; private set; }
    public String QuoteSourceName { get; private set; } = "";
    public String QuoteType { get; private set; } = "";
    public String Region { get; private set; } = "";
    public Decimal? RegularMarketChange { get; private set; }
    public Decimal? RegularMarketChangePercent { get; private set; }
    public Decimal? RegularMarketDayHigh { get; private set; }
    public Decimal? RegularMarketDayLow { get; private set; }
    public String RegularMarketDayRange { get; private set; } = "";
    public Decimal? RegularMarketOpen { get; private set; }
    public Decimal? RegularMarketPreviousClose { get; private set; }
    public Decimal? RegularMarketPrice { get; private set; }
    public ZonedDateTime RegularMarketTime { get; private set; }
    public Int64 RegularMarketTimeSeconds { get; private set; }
    public Int64? RegularMarketVolume { get; private set; }
    public Int64 SharesOutstanding { get; private set; }
    public String ShortName { get; private set; } = "";
    public Int64? SourceInterval { get; private set; }
    public Result<SplitTick[]> SplitHistory { get; internal set; }
    public Symbol Symbol { get; private set; }
    public Boolean? Tradeable { get; private set; }
    public Decimal? TrailingAnnualDividendRate { get; private set; }
    public Decimal? TrailingAnnualDividendYield { get; private set; }
    public Decimal? TrailingPE { get; private set; }
    public Decimal? TrailingThreeMonthNavReturns { get; private set; }
    public Decimal? TrailingThreeMonthReturns { get; private set; }
    public Boolean? Triggerable { get; private set; }
    public Decimal? TwoHundredDayAverage { get; private set; }
    public Decimal? TwoHundredDayAverageChange { get; private set; }
    public Decimal? TwoHundredDayAverageChangePercent { get; private set; }
    public String TypeDisp { get; private set; } = "";
    public Decimal? YtdReturn { get; private set; }
}

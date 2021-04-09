using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace YahooQuotesApi
{
    public class Security
    {
        private static readonly IDateTimeZoneProvider DateTimeZoneProvider = DateTimeZoneProviders.Tzdb;

        internal Security(Symbol symbol) => Symbol = symbol;

        internal Security(JsonElement jsonElement, ILogger logger)
        {
            foreach (var jproperty in jsonElement.EnumerateObject())
                SetProperty(jproperty, logger);

            if (Symbol.IsEmpty)
                throw new InvalidDataException("Security: no symbol.");
            if (Currency != "")
            {
                if (Symbol.TryCreate(Currency) is null)
                    logger.LogWarning($"Invalid currency value: '{Currency}'.");
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
            if (ExchangeTimezoneName != "")
                ExchangeTimezone = DateTimeZoneProvider.GetZoneOrNull(ExchangeTimezoneName);
            if (RegularMarketTimeSeconds > 0 && ExchangeTimezone != null)
                RegularMarketTime = Instant.FromUnixTimeSeconds(RegularMarketTimeSeconds).InZone(ExchangeTimezone);
            if (PreMarketTimeSeconds > 0 && ExchangeTimezone != null)
                PreMarketTime = Instant.FromUnixTimeSeconds(PreMarketTimeSeconds).InZone(ExchangeTimezone);
            if (PostMarketTimeSeconds > 0 && ExchangeTimezone != null)
                 PostMarketTime = Instant.FromUnixTimeSeconds(PostMarketTimeSeconds).InZone(ExchangeTimezone);
            if (FirstTradeDateMilliseconds > 0)
                FirstTradeDate = Instant.FromUnixTimeMilliseconds(FirstTradeDateMilliseconds).InUtc().LocalDateTime;
        }

        private void SetProperty(JsonProperty jproperty, ILogger logger)
        {
            var jName = jproperty.Name switch
            {
                "regularMarketTime" => "RegularMarketTimeSeconds",
                "preMarketTime" => "PreMarketTimeSeconds",
                "postMarketTime" => "PostMarketTimeSeconds",
                "dividendDate" => "DividendDateSeconds",
                _ => jproperty.Name.ToPascal()
            };

            PropertyInfo? propertyInfo = GetType().GetProperty(jName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (propertyInfo != null)
            {
                var value = GetJsonPropertValueOfType(jproperty, propertyInfo.PropertyType) ?? throw new Exception();
                if (propertyInfo.Name == "Symbol")
                {
                    var symbol = (string)value;
                    if (symbol.EndsWith("=X") && symbol.Length == 5)
                        symbol = "USD" + symbol;
                    logger.LogTrace($"Setting security property: Symbol = {symbol}");
                    Symbol = symbol.ToSymbol();
                    return;
                }
                logger.LogTrace($"Setting security property: {propertyInfo.Name} = {value}");
                propertyInfo.SetValue(this, value);
                return;
            }
            var val = GetJsonPropertyValue(jproperty);
            logger.LogTrace($"Setting security other property: {jName} = {val}");
            OtherProperties.Add(jName, val);
        }
        private static object? GetJsonPropertValueOfType(JsonProperty jproperty, Type propertyType)
        {
            var value = jproperty.Value;
            var kind = value.ValueKind;
            if (kind == JsonValueKind.String)
                return value.GetString();
            if (kind == JsonValueKind.True || kind == JsonValueKind.False)
                return value.GetBoolean();
            if (kind == JsonValueKind.Number)
            {
                if (propertyType == typeof(Int64) || propertyType == typeof(Int64?))
                    return value.GetInt64();
                if (propertyType == typeof(Double) || propertyType == typeof(Double?))
                    return value.GetDouble();
                if (propertyType == typeof(Decimal) || propertyType == typeof(Decimal?))
                    return value.GetDecimal();
            }
            throw new InvalidDataException($"Unsupported type: {propertyType} for property: {jproperty.Name}.");
        }

        private static object? GetJsonPropertyValue(JsonProperty jproperty)
        {
            var value = jproperty.Value;
            var type = value.ValueKind;
            if (type == JsonValueKind.String)
                return value.GetString();
            if (type == JsonValueKind.True || type == JsonValueKind.False)
                return value.GetBoolean();
            if (type == JsonValueKind.Number)
            {
                if (value.TryGetInt64(out var l))
                    return l;
                if (value.TryGetDouble(out var dbl))
                    return dbl;
            }
            return value.GetRawText();
        }

        public Decimal Ask { get; private set; }
        public Int64 AskSize { get; private set; }
        public Int64 AverageDailyVolume10Day { get; private set; }
        public Int64 AverageDailyVolume3Month { get; private set; }
        public Decimal Bid { get; internal set; }
        public Int64 BidSize { get; private set; }
        public Decimal BookValue { get; private set; }
        public String Currency { get; internal set; } = "";
        public String DisplayName { get; private set; } = "";
        public LocalDateTime DividendDate { get; }
        public Int64 DividendDateSeconds { get; private set; }
        public Result<DividendTick[]> DividendHistory { get; internal set; } = Result<DividendTick[]>.Nothing();
        public LocalDateTime EarningsTime { get; }
        public LocalDateTime EarningsTimeEnd { get; }
        public Int64 EarningsTimestamp { get; private set; }
        public Int64 EarningsTimestampEnd { get; private set; }
        public Int64 EarningsTimestampStart { get; private set; }
        public LocalDateTime EarningsTimeStart { get; }
        public Decimal? EpsCurrentYear { get; private set; }
        public Decimal? EpsForward { get; private set; }
        public Decimal? EpsTrailingTwelveMonths { get; private set; }
        public Boolean? EsgPopulated { get; private set; }
        public String Exchange { get; private set; } = "";
        public LocalTime ExchangeCloseTime { get; }
        public Int64? ExchangeDataDelayedBy { get; private set; }
        public DateTimeZone? ExchangeTimezone { get; }
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
        public LocalDateTime FirstTradeDate { get; }
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
        public ZonedDateTime PostMarketTime { get; }
        public Int64 PostMarketTimeSeconds { get; private set; }
        public Decimal? PreMarketChange { get; private set; }
        public Decimal? PreMarketChangePercent { get; private set; }
        public Decimal? PreMarketPrice { get; private set; }
        public ZonedDateTime PreMarketTime { get; }
        public Int64 PreMarketTimeSeconds { get; private set; }
        public Decimal? PriceEpsCurrentYear { get; private set; }
        public Int64? PriceHint { get; private set; }
        public Result<CandleTick[]> PriceHistory { get; internal set; } = Result<CandleTick[]>.Nothing();
        public Result<PriceTick[]> PriceHistoryBase { get; internal set; } = Result<PriceTick[]>.Nothing();
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
        public ZonedDateTime RegularMarketTime { get; }
        public Int64 RegularMarketTimeSeconds { get; private set; }
        public Int64? RegularMarketVolume { get; private set; }
        public Int64 SharesOutstanding { get; private set; }
        public String ShortName { get; private set; } = "";
        public Int64? SourceInterval { get; private set; }
        public Result<SplitTick[]> SplitHistory { get; internal set; } = Result<SplitTick[]>.Nothing();
        public Symbol Symbol { get; private set; } = Symbol.Empty;
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
        public Decimal? YtdReturn { get; private set; }
        public Dictionary<string, object?> OtherProperties { get; } = new Dictionary<string, object?>();
    }
}

using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Collections.Generic;

namespace YahooQuotesApi
{
    public class Security
    {
        public IReadOnlyDictionary<string, object> Fields { get; }
        internal Security(Dictionary<string, object> fields, ILogger logger)
        {
            Fields = fields;
            foreach (var field in fields)
            {
                var property = GetType().GetProperty(field.Key);
                if (property != null)
                    property.SetValue(this, field.Value);
                else
                {
                    var symbol = (string) fields["Symbol"];
                    logger.LogTrace($"Security property not defined for field '{field}' on symbol '{symbol}'.");
                }
            }
        }

        public dynamic? this[string fieldName] =>
            fieldName == null ? throw new ArgumentNullException(nameof(fieldName)) :
            (Fields.TryGetValue(fieldName, out dynamic val) ? val : null);

        // Security.cs: 90. This list was generated automatically from names defined by Yahoo, mostly.
        public Decimal? Ask { get; private set; }
        public Int64? AskSize { get; private set; }
        public Int64? AverageDailyVolume10Day { get; private set; }
        public Int64? AverageDailyVolume3Month { get; private set; }
        public Decimal? Bid { get; private set; }
        public Int64? BidSize { get; private set; }
        public Decimal? BookValue { get; private set; }
        public String Currency { get; private set; } = "";
        public String DisplayName { get; private set; } = "";
        public LocalDateTime? DividendDate { get; private set; }
        public Int64? DividendDateSeconds { get; private set; }
        public IReadOnlyList<DividendTick> DividendHistory { get; private set; } = new List<DividendTick>();
        public LocalDateTime? EarningsTime { get; private set; }
        public LocalDateTime? EarningsTimeEnd { get; private set; }
        public Int64? EarningsTimestamp { get; private set; }
        public Int64? EarningsTimestampEnd { get; private set; }
        public Int64? EarningsTimestampStart { get; private set; }
        public LocalDateTime? EarningsTimeStart { get; private set; }
        public Decimal? EpsForward { get; private set; }
        public Decimal? EpsTrailingTwelveMonths { get; private set; }
        public Boolean? EsgPopulated { get; private set; }
        public String Exchange { get; private set; } = "";
        public LocalTime? ExchangeCloseTime { get; private set; }
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
        public LocalDateTime? FirstTradeDate { get; private set; }
        public Int64? FirstTradeDateMilliseconds { get; private set; }
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
        public ZonedDateTime? PostMarketTime { get; private set; }
        public Int64? PostMarketTimeSeconds { get; private set; }
        public Decimal? PreMarketChange { get; private set; }
        public Decimal? PreMarketChangePercent { get; private set; }
        public Decimal? PreMarketPrice { get; private set; }
        public ZonedDateTime? PreMarketTime { get; private set; }
        public Int64? PreMarketTimeSeconds { get; private set; }
        public Int64? PriceHint { get; private set; }
        public IReadOnlyList<PriceTick> PriceHistory { get; private set; } = new List<PriceTick>();
        public IReadOnlyList<PriceTick> PriceHistoryBase { get; private set; } = new List<PriceTick>();
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
        public ZonedDateTime? RegularMarketTime { get; private set; }
        public Int64? RegularMarketTimeSeconds { get; private set; }
        public Int64? RegularMarketVolume { get; private set; }
        public Int64? SharesOutstanding { get; private set; }
        public String ShortName { get; private set; } = "";
        public Int64? SourceInterval { get; private set; }
        public IReadOnlyList<SplitTick> SplitHistory { get; private set; } = new List<SplitTick>();
        public String Symbol { get; private set; } = "";
        public Boolean? Tradeable { get; private set; }
        public Decimal? TrailingAnnualDividendRate { get; private set; }
        public Decimal? TrailingAnnualDividendYield { get; private set; }
        public Decimal? TrailingPE { get; private set; }
        public Boolean? Triggerable { get; private set; }
        public Decimal? TwoHundredDayAverage { get; private set; }
        public Decimal? TwoHundredDayAverageChange { get; private set; }
        public Decimal? TwoHundredDayAverageChangePercent { get; private set; }
    }
}

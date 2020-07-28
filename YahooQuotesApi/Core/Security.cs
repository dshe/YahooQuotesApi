using NodaTime;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YahooQuotesApi
{
    public class Security
    {
        public IReadOnlyDictionary<string, object> Fields { get; }
        internal Security(Dictionary<string, object> fields) => Fields = fields;

        public dynamic? this[string fieldName] => GetD(fieldName);

        private dynamic? GetD([CallerMemberName] string? name = null)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            return Fields.TryGetValue(name, out dynamic val) ? val : null;
        }
        private string GetS([CallerMemberName] string? name = null)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            return Fields.TryGetValue(name, out dynamic val) ? val : "";
        }

        // Security.cs: 89. This list was generated automatically from names defined by Yahoo, mostly.
        public Decimal? Ask => GetD();
        public Int64? AskSize => GetD();
        public Int64? AverageDailyVolume10Day => GetD();
        public Int64? AverageDailyVolume3Month => GetD();
        public Decimal? Bid => GetD();
        public Int64? BidSize => GetD();
        public Decimal? BookValue => GetD();
        public String Currency => GetS();
        public String DisplayName => GetS();
        public LocalDateTime? DividendDate => GetD();
        public Int64? DividendDateSeconds => GetD();
        public IReadOnlyList<DividendTick>? DividendHistory => GetD();
        public LocalDateTime? EarningsTime => GetD();
        public LocalDateTime? EarningsTimeEnd => GetD();
        public Int64? EarningsTimestamp => GetD();
        public Int64? EarningsTimestampEnd => GetD();
        public Int64? EarningsTimestampStart => GetD();
        public LocalDateTime? EarningsTimeStart => GetD();
        public Decimal? EpsForward => GetD();
        public Decimal? EpsTrailingTwelveMonths => GetD();
        public Boolean? EsgPopulated => GetD();
        public String Exchange => GetS();
        public LocalTime? ExchangeCloseTime => GetD();
        public Int64? ExchangeDataDelayedBy => GetD();
        public DateTimeZone? ExchangeTimezone => GetD();
        public String ExchangeTimezoneName => GetS();
        public String ExchangeTimezoneShortName => GetS();
        public Decimal? FiftyDayAverage => GetD();
        public Decimal? FiftyDayAverageChange => GetD();
        public Decimal? FiftyDayAverageChangePercent => GetD();
        public Decimal? FiftyTwoWeekHigh => GetD();
        public Decimal? FiftyTwoWeekHighChange => GetD();
        public Decimal? FiftyTwoWeekHighChangePercent => GetD();
        public Decimal? FiftyTwoWeekLow => GetD();
        public Decimal? FiftyTwoWeekLowChange => GetD();
        public Decimal? FiftyTwoWeekLowChangePercent => GetD();
        public String FiftyTwoWeekRange => GetS();
        public String FinancialCurrency => GetS();
        public LocalDateTime? FirstTradeDate => GetD();
        public Int64? FirstTradeDateMilliseconds => GetD();
        public Decimal? ForwardPE => GetD();
        public String FullExchangeName => GetS();
        public Int64? GmtOffSetMilliseconds => GetD();
        public String Language => GetS();
        public String LongName => GetS();
        public String Market => GetS();
        public Int64? MarketCap => GetD();
        public String MarketState => GetS();
        public String MessageBoardId => GetS();
        public Decimal? PostMarketChange => GetD();
        public Decimal? PostMarketChangePercent => GetD();
        public Decimal? PostMarketPrice => GetD();
        public ZonedDateTime? PostMarketTime => GetD();
        public Int64? PostMarketTimeSeconds => GetD();
        public Decimal? PreMarketChange => GetD();
        public Decimal? PreMarketChangePercent => GetD();
        public Decimal? PreMarketPrice => GetD();
        public ZonedDateTime? PreMarketTime => GetD();
        public Int64? PreMarketTimeSeconds => GetD();
        public Int64? PriceHint => GetD();
        public IReadOnlyList<PriceTick>? PriceHistory => GetD();
        public IReadOnlyList<PriceTick>? PriceHistoryBase => GetD();
        public Decimal? PriceToBook => GetD();
        public String QuoteSourceName => GetS();
        public String QuoteType => GetS();
        public String Region => GetS();
        public Decimal? RegularMarketChange => GetD();
        public Decimal? RegularMarketChangePercent => GetD();
        public Decimal? RegularMarketDayHigh => GetD();
        public Decimal? RegularMarketDayLow => GetD();
        public String RegularMarketDayRange => GetS();
        public Decimal? RegularMarketOpen => GetD();
        public Decimal? RegularMarketPreviousClose => GetD();
        public Decimal? RegularMarketPrice => GetD();
        public ZonedDateTime? RegularMarketTime => GetD();
        public Int64? RegularMarketTimeSeconds => GetD();
        public Int64? RegularMarketVolume => GetD();
        public Int64? SharesOutstanding => GetD();
        public String ShortName => GetS();
        public Int64? SourceInterval => GetD();
        public IReadOnlyList<SplitTick>? SplitHistory => GetD();
        public String Symbol => GetS();
        public Boolean? Tradeable => GetD();
        public Decimal? TrailingAnnualDividendRate => GetD();
        public Decimal? TrailingAnnualDividendYield => GetD();
        public Decimal? TrailingPE => GetD();
        public Boolean? Triggerable => GetD();
        public Decimal? TwoHundredDayAverage => GetD();
        public Decimal? TwoHundredDayAverageChange => GetD();
        public Decimal? TwoHundredDayAverageChangePercent => GetD();
    }
}

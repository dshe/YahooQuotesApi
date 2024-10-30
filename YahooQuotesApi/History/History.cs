using System.Collections.Immutable;
using System.Collections.ObjectModel;
namespace YahooQuotesApi;

public sealed class History
{
    public Symbol Symbol { get; internal set; }
    public Symbol Currency { get; internal set; }
    public string ShortName { get; internal set; } = "";
    public string LongName { get; internal set; } = "";
    public string ExchangeName { get; internal set; } = "";
    public string FullExchangeName { get; internal set; } = "";
    public string InstrumentType { get; internal set; } = "";
    public string Timezone { get; internal set; } = "";
    public string ExchangeTimezoneName { get; internal set; } = "";
    public int? Gmtoffset { get; internal set; }
    public Instant FirstTradeDate { get; internal set; }
    public Instant RegularMarketTime { get; internal set; }
    public decimal RegularMarketPrice { get; internal set; }
    public decimal RegularMarketDayHigh { get; internal set; }
    public decimal RegularMarketDayLow { get; internal set; }
    public long RegularMarketVolume { get; internal set; }
    public decimal ChartPreviousClose { get; internal set; }
    public decimal PreviousClose { get; internal set; }
    public decimal FiftyTwoWeekHigh { get; internal set; }
    public decimal FiftyTwoWeekLow { get; internal set; }
    public string DataGranularity { get; internal set; } = "";
    public string Range { get; internal set; } = "";
    public int Scale { get; internal set; }
    public int PriceHint { get; internal set; }
    public bool HasPrePostMarketData { get; internal set; }
    public ImmutableArray<Tick> Ticks { get; internal set; } = [];
    public ImmutableArray<BaseTick> BaseTicks { get; internal set; } = [];
    public ImmutableArray<Dividend> Dividends { get; internal set; } = [];
    public ImmutableArray<Split> Splits { get; internal set; } = [];
    public ImmutableArray<TradingPeriod> CurrentTradingPeriod { get; internal set; } = [];
    public IReadOnlyDictionary<string, object?> Properties { get; internal set; } = ReadOnlyDictionary<string, object?>.Empty;
}


// Tisk date is the start of trading.
public sealed record Tick(Instant Date, double Open, double High, double Low, double Close, double AdjustedClose, long Volume);
public sealed record BaseTick(Instant Date, double Price, long Volume); // 28 bytes
public sealed record Dividend(Instant Date, decimal Amount);
public sealed record Split(Instant Date, decimal Numerator, decimal Denominator);
public sealed record TradingPeriod(string Name, Instant StartDate, Instant EndDate, int Gmtoffset, string Timezone);

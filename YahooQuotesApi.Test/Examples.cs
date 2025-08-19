using NodaTime;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Xunit;
namespace YahooQuotesApi.Examples;

public class Examples
{
    [Fact]
    public async Task GetSnapshot()
    {
        YahooQuotes yahooQuotes = new YahooQuotesBuilder().Build();

        Snapshot? snapshot = await yahooQuotes.GetSnapshotAsync("AAPL") 
            ?? throw new ArgumentException("Unknown symbol.");
        Assert.Equal("Apple Inc.", snapshot.LongName);
        Assert.True(snapshot.RegularMarketPrice > 0);
    }

    [Fact]
    public async Task GetSnapshots()
    {
        YahooQuotes yahooQuotes = new YahooQuotesBuilder().Build();

        Dictionary<string, Snapshot?> snapshots = await yahooQuotes.GetSnapshotAsync(["AAPL", "BP.L", "USDJPY=X"]);

        Snapshot snapshot = snapshots["BP.L"] ?? throw new ArgumentException("Unknown symbol.");

        Assert.Equal("BP p.l.c.", snapshot.LongName);
        Assert.Equal("GBP=X", snapshot.Currency.Name);
        Assert.True(snapshot.RegularMarketPrice > 0);
    }

    [Fact]
    public async Task GetHistory()
    {
        YahooQuotes yahooQuotes = new YahooQuotesBuilder()
            .WithHistoryStartDate(Instant.FromUtc(2024, 10, 1, 0, 0))
            .Build();

        Result<History> result = await yahooQuotes.GetHistoryAsync("MSFT");
        History history = result.Value;

        Assert.Equal("Microsoft Corporation", history.LongName);
        Assert.Equal("USD=X", history.Currency.Name);
        Assert.Equal("America/New_York", history.ExchangeTimezoneName);
        DateTimeZone tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(history.ExchangeTimezoneName) ??
            throw new ArgumentNullException("Unknown timezone");

        ImmutableArray<Tick> ticks = history.Ticks;
        Tick firstTick = ticks[0];
        ZonedDateTime zdt = firstTick.Date.InZone(tz);
        // Note that tick time is market open of 9:30.
        Assert.Equal(new LocalDateTime(2024, 10, 1, 9, 30, 0), zdt.LocalDateTime);
        Assert.Equal(420.69, firstTick.Close, 2); // in USD
    }
        
    [Fact]
    public async Task GetHistoryInBaseCurrency()
    {
        YahooQuotes yahooQuotes = new YahooQuotesBuilder()
            .WithHistoryStartDate(Instant.FromUtc(2024, 10, 1, 0, 0))
            .Build();

        Result<History> result = await yahooQuotes.GetHistoryAsync("ASML.AS", "USD=X");
        History history = result.Value;

        Assert.Equal("ASML Holding N.V.", history.LongName);
        Assert.Equal("EUR=X", history.Currency.Name);
        Assert.Equal("Europe/Amsterdam", history.ExchangeTimezoneName);
        DateTimeZone tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(history.ExchangeTimezoneName)
            ?? throw new ArgumentException("Unknown timezone.");

        BaseTick firstBaseTick = history.BaseTicks[0];
        Instant instant = firstBaseTick.Date;
        ZonedDateTime zdt = instant.InZone(tz);
        Assert.Equal(new LocalDateTime(2024, 10, 1, 17, 30, 0).InZoneLeniently(tz), zdt);
        Assert.Equal(814.01, firstBaseTick.Price, 2); // in USD
    }
}

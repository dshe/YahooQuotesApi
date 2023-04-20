using NodaTime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace YahooQuotesApi.Tests;

public class Examples
{
    [Fact]
    public async Task Snapshot()
    {
        YahooQuotes yahooQuotes = new YahooQuotesBuilder().Build();

        Security security = await yahooQuotes.GetAsync("AAPL") 
            ?? throw new ArgumentException("Unknown symbol: AAPL.");
        Assert.Equal("Apple Inc.", security.LongName);
        Assert.True(security.RegularMarketPrice > 0);
    }

    [Fact]
    public async Task Snapshots()
    {
        YahooQuotes yahooQuotes = new YahooQuotesBuilder().Build();

        Dictionary<string, Security?> securities = await yahooQuotes.GetAsync(new[] { "AAPL", "BP.L", "USDJPY=X" });

        Security security = securities["BP.L"] ?? throw new ArgumentException("Unknown symbol");

        Assert.Equal("BP p.l.c.", security.LongName);
        Assert.Equal("GBP", security.Currency, true);
        Assert.Equal("LSE", security.Exchange);
        Assert.True(security.RegularMarketPrice > 0);
    }

    [Fact]
    public async Task SnapshotWithPriceHistory()
    {
        YahooQuotes yahooQuotes = new YahooQuotesBuilder()
            .WithHistoryStartDate(Instant.FromUtc(2020, 1, 1, 0, 0))
            .Build();

        Security security = await yahooQuotes.GetAsync("MSFT", Histories.PriceHistory) ??
            throw new ArgumentException("Unknown symbol: MSFT.");

        Assert.Equal("NasdaqGS", security.FullExchangeName);

        Assert.False(security.PriceHistory.HasError);
        PriceTick[] priceHistory = security.PriceHistory.Value;

        PriceTick tick = priceHistory[0];
        Assert.Equal(new LocalDate(2020, 1, 2), tick.Date);
        Assert.Equal(160.62, tick.Close);
    }

    [Fact]
    public async Task SnapshotWithPriceHistoryInBaseCurrency()
    {
        YahooQuotes yahooQuotes = new YahooQuotesBuilder()
            .WithHistoryStartDate(Instant.FromUtc(2020, 7, 15, 0, 0))
            .WithCacheDuration(snapshotCacheDuration: Duration.FromMinutes(30), historyCacheDuration: Duration.FromHours(6))
            .Build();

        Security security = await yahooQuotes
            .GetAsync("TSLA", Histories.PriceHistory, historyBase: "JPY=X")
                ?? throw new ArgumentException("Unknown symbol: TSLA.");

        Assert.Equal("Tesla, Inc.", security.ShortName);
        Assert.Equal("USD", security.Currency);
        Assert.Equal("America/New_York", security.ExchangeTimezone?.Id);

        PriceTick tick = security.PriceHistory.Value[0];
        Assert.Equal(new LocalDate(2020, 7, 15), tick.Date);
        Assert.Equal(103.0673, tick.Close); // in USD

        Instant instant = new LocalDateTime(2020, 7, 15, 16, 0, 0)
            .InZoneLeniently(security.ExchangeTimezone!).ToInstant();

        ValueTick tickBase = security.PriceHistoryBase.Value[0];
        Assert.Equal(instant, tickBase.Date);
        Assert.Equal(11046, tickBase.Value, 0); // in JPY
    }
}

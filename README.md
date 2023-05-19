# YahooQuotesApi&nbsp;&nbsp; [![Build status](https://ci.appveyor.com/api/projects/status/qx83p28cdqvcpbhm?svg=true)](https://ci.appveyor.com/project/dshe/yahooquotesapi) [![NuGet](https://img.shields.io/nuget/vpre/YahooQuotesApi.svg)](https://www.nuget.org/packages/YahooQuotesApi/) [![NuGet](https://img.shields.io/nuget/dt/YahooQuotesApi?color=orange)](https://www.nuget.org/packages/YahooQuotesApi/) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![Ukraine](https://raw.githubusercontent.com/vshymanskyy/StandWithUkraine/main/badges/StandWithUkraine.svg)](https://stand-with-ukraine.pp.ua)


**Retrieves from Yahoo Finance: quote snapshots, historical quotes, dividends, splits, and modules**
- **.NET 6.0** library
- intellisense support for properties
- simple and intuitive API
- fault-tolerant
- tested
- dependencies: Polly, NodaTime

### Installation
```bash
PM> Install-Package YahooQuotesApi
```

### Examples
#### snapshot
```csharp
using NodaTime;
using YahooQuotesApi;

YahooQuotes yahooQuotes = new YahooQuotesBuilder().Build();

Security? security = await yahooQuotes.GetAsync("AAPL");

if (security is null)
    throw new ArgumentException("Unknown symbol: AAPL.");

Assert.Equal("Apple Inc.", security.LongName);
Assert.True(security.RegularMarketPrice > 0);
```

#### snapshots
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder().Build();

Dictionary<string,Security?> securities = await yahooQuotes.GetAsync(new[] { "AAPL", "BP.L", "USDJPY=X" });

Security security = securities["BP.L"] ?? throw new ArgumentException("Unknown symbol");

Assert.Equal("BP p.l.c.", security.LongName);
Assert.Equal("GBP", security.Currency, true);
Assert.Equal("LSE", security.Exchange);
Assert.True(security.RegularMarketPrice > 0);
```

#### snapshot with price history
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder()
    .WithHistoryStartDate(Instant.FromUtc(2020, 1, 1, 0, 0))
    .Build();

Security security = await yahooQuotes.GetAsync("MSFT", Histories.PriceHistory)
    ?? throw new ArgumentException("Unknown symbol.");

Assert.Equal("NasdaqGS", security.FullExchangeName);

Assert.False(security.PriceHistory.HasError);
PriceTick[] priceHistory = security.PriceHistory.Value;
PriceTick tick = priceHistory[0];

Assert.Equal(new LocalDate(2020, 1, 2), tick.Date);
Assert.Equal(160.62, tick.Close);
```

#### snapshot with price history in base currency
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder()
    .WithHistoryStartDate(Instant.FromUtc(2020, 7, 15, 0, 0))
    .WithCacheDuration(snapshotCacheDuration: Duration.FromMinutes(30), historyCacheDuration: Duration.FromHours(6))
    .Build();

Security security = await yahooQuotes
    .GetAsync("TSLA", HistoryFlags.PriceHistory, historyBase: "JPY=X")
        ?? throw new ArgumentException("Unknown symbol.");

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
```

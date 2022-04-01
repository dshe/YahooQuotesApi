# YahooQuotesApi&nbsp;&nbsp; [![Build status](https://ci.appveyor.com/api/projects/status/qx83p28cdqvcpbhm?svg=true)](https://ci.appveyor.com/project/dshe/yahooquotesapi) [![NuGet](https://img.shields.io/nuget/vpre/YahooQuotesApi.svg)](https://www.nuget.org/packages/YahooQuotesApi/) [![NuGet](https://img.shields.io/nuget/dt/YahooQuotesApi?color=orange)](https://www.nuget.org/packages/YahooQuotesApi/) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Retrieves snapshot quotes and historical quotes, dividends and splits from Yahoo Finance**
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
    .HistoryStarting(Instant.FromUtc(2020, 1, 1, 0, 0))
    .Build();

Security security = await yahooQuotes.GetAsync("MSFT", HistoryFlags.PriceHistory)
    ?? throw new ArgumentException("Unknown symbol.");

Assert.Equal("NasdaqGS", security.FullExchangeName);

CandleTick[] priceHistory = security.PriceHistory.Value;
CandleTick tick = priceHistory[0];

Assert.Equal(new LocalDate(2020, 1, 2), tick.Date);
Assert.Equal(160.62, tick.Close);
```

#### snapshot with price history in base currency
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder()
    .HistoryStarting(Instant.FromUtc(2020, 7, 15, 0, 0))
    .WithCaching(snapshotDuration: Duration.FromMinutes(30), historyDuration: Duration.FromHours(6))
    .Build();

Security security = await yahooQuotes
    .GetAsync("TSLA", HistoryFlags.PriceHistory, historyBase: "JPY=X")
        ?? throw new ArgumentException("Unknown symbol.");

Assert.Equal("Tesla, Inc.", security.ShortName);
Assert.Equal("USD", security.Currency);
Assert.Equal("America/New_York", security.ExchangeTimezone?.Id);

CandleTick tick = security.PriceHistory.Value[0];
Assert.Equal(new LocalDate(2020, 7, 15), tick.Date);
Assert.Equal(309.202, tick.Close); // in USD

Instant instant = new LocalDateTime(2020, 7, 15, 16, 0, 0)
    .InZoneLeniently(security.ExchangeTimezone!).ToInstant();

ValueTick tickBase = security.PriceHistoryBase.Value[0];
Assert.Equal(instant, tickBase.Date);
Assert.Equal(33139, tickBase.Value, 0); // in JPY
```

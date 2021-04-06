## YahooQuotesApi&nbsp;&nbsp; [![Build status](https://ci.appveyor.com/api/projects/status/qx83p28cdqvcpbhm?svg=true)](https://ci.appveyor.com/project/dshe/yahooquotesapi) [![NuGet](https://img.shields.io/nuget/vpre/YahooQuotesApi.svg)](https://www.nuget.org/packages/YahooQuotesApi/) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Yahoo Finance API to retrieve quote snapshots and historical quotes, dividends and splits**
- supports **.NET Standard 2.0**
- dependencies: NodaTime, Polly
- simple and intuitive API
- fault-tolerant
- tested
```csharp
using NodaTime;
using YahooQuotesApi;
```
### snapshots
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder().Build();

Security? security = await yahooQuotes.GetAsync("AAPL");
if (security == null)
    throw new ArgumentException("Unknown symbol: AAPL.");

Assert.Equal("Apple Inc.", security.LongName);
Assert.True(security.RegularMarketPrice > 0);
```
### snapshots with history
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder()
    .HistoryStarting(Instant.FromUtc(2020, 1, 1, 0, 0))
    .Build();

Security security = await yahooQuotes.GetAsync("MSFT", HistoryFlags.PriceHistory) ??
    throw new ArgumentException("Unknown symbol: MSFT.");

Assert.Equal("NasdaqGS", security.FullExchangeName);

CandleTick[] priceHistory = security.PriceHistory.Value;

CandleTick tick = priceHistory[0];
Assert.Equal(new LocalDate(2020, 1, 2), tick.Date);
Assert.Equal(160.62, tick.Close);
```
### snapshots with history in a base currency
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder()
    .HistoryStarting(Instant.FromUtc(2020, 7, 15, 0, 0))
    .UseNonAdjustedClose() // for testing
    .Build();

Security security = await yahooQuotes
    .GetAsync("TSLA", HistoryFlags.PriceHistory, historyBase: "JPY=X")
        ?? throw new ArgumentException("Unknown symbol: TSLA.");

Assert.Equal("Tesla, Inc.", security.ShortName);
Assert.Equal("USD", security.Currency);

CandleTick tick = security.PriceHistory.Value[0];
Assert.Equal(new LocalDate(2020, 7, 15), tick.Date);
Assert.Equal(309.202, tick.Close); // in USD

PriceTick tickBase = security.PriceHistoryBase.Value[0];
Assert.Equal(new LocalDateTime(2020, 7, 15, 16, 0, 0), tickBase.Date.LocalDateTime);
Assert.Equal(33139, tickBase.Price, 0); // in JPY
```

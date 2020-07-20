## YahooQuotesApi&nbsp;&nbsp; [![Build status](https://ci.appveyor.com/api/projects/status/qx83p28cdqvcpbhm?svg=true)](https://ci.appveyor.com/project/dshe/yahooquotesapi) [![NuGet](https://img.shields.io/nuget/vpre/YahooQuotesApi.svg)](https://www.nuget.org/packages/YahooQuotesApi/) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Yahoo Finance API to retrieve quote snapshots, and quote, dividend and split history**
- asynchronous
- supports **.NET Standard 2.0**
- dependencies: NodaTime, Flurl, CsvHelper
- simple and intuitive API
- tested
```csharp
using NodaTime;
using YahooQuotesApi;
```
### snapshots
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder().Build();

IReadOnlyDictionary<string, Security?> securities =
    await yahooQuotes.GetAsync(new[] { "AAPL", "X" });

Assert.Equal(2, securities.Count);

Security? security = securities["AAPL"];
if (security == null)
    throw new ArgumentException("Unknown symbol: AAPL.");

Assert.Equal("Apple Inc.", security.LongName);
Assert.True(security.RegularMarketPrice > 0);
```
### history
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder()
    .WithPriceHistory()
    .WithDividendHistory()
    .WithSplitHistory()
    .Build();

Security? security = await yahooQuotes.GetAsync("MSFT");

Assert.True(security!.RegularMarketPrice > 0);
Assert.Equal("NasdaqGS", security!.FullExchangeName);

IReadOnlyList<DividendTick>? dividendHistory = security.DividendHistory;
Assert.Equal(new LocalDate(2003, 2, 19), dividendHistory![0].Date);
Assert.Equal(0.08, dividendHistory[0].Dividend);

IReadOnlyList<SplitTick>? splitHistory = security.SplitHistory;
Assert.Equal(new LocalDate(2003, 2, 18), splitHistory![0].Date);
Assert.Equal(1, splitHistory[0].BeforeSplit);
Assert.Equal(2, splitHistory[0].AfterSplit);

IReadOnlyList<PriceTick>? priceHistory = security.PriceHistory;
PriceTick tick = priceHistory![0];
ZonedDateTime zdt = tick.Date;
Assert.Equal("America/New_York", zdt.Zone.Id);
Assert.Equal(new LocalDate(2000, 1, 3), zdt.Date);
Assert.Equal(new LocalTime(16, 0, 0), zdt.TimeOfDay);
Assert.Equal(58.28125, tick.Close);
```
### currency rates
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder()
    .WithPriceHistory()
    .HistoryStarting(Instant.FromUtc(2020, 1, 1, 0, 0))
    .Build();

Security? security = await yahooQuotes.GetAsync("EURUSD=X");

Assert.Equal("EURUSD=X", security!.Symbol);
Assert.Equal("EUR/USD", security.ShortName);
Assert.Equal("USD", security.Currency); // base currency
Assert.True(security.RegularMarketPrice > 0);

PriceTick tick = security.PriceHistory!.First();
Assert.Equal("Europe/London", tick.Date.Zone.Id);
Assert.Equal(new LocalDateTime(2020, 1, 1, 16, 0, 0), tick.Date.LocalDateTime);
Assert.Equal(1.122083, tick.Close);
```
### history in base currency
```csharp
var security = await new YahooQuotesBuilder()
    .WithPriceHistory(baseCurrency: "JPY")
    .HistoryStarting(Instant.FromUtc(2020, 7, 15, 0, 0))
    .Build()
    .GetAsync("TSLA")
    ?? throw new ArgumentException("Unknown symbol: TSLA.");

Assert.Equal("Tesla, Inc.", security.ShortName);
Assert.Equal("USD", security.Currency);
Assert.True(security.RegularMarketPrice > 0);

PriceTick tick = security.PriceHistory?.First() ?? throw new ArgumentException();
Assert.Equal(new LocalDateTime(2020, 7, 15, 16, 0, 0), tick.Date.LocalDateTime);
Assert.Equal(165696.20317687377, tick.Close); // in JPY
```

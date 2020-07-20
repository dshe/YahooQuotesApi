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
    await yahooQuotes.GetAsync(new[] { "IBM", "AAPL" });

Assert.Equal(2, securities.Count);

Security? security = securities["IBM"];
if (security == null)
    throw new ArgumentException("Unknown symbol: IBM.");

Assert.Equal("International Business Machines Corporation", security.LongName);
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

Assert.Equal("NasdaqGS", security!.FullExchangeName);

IReadOnlyList<PriceTick>? priceHistory = security.PriceHistory;
Assert.Equal(58.28125, priceHistory![0].Close);

IReadOnlyList<DividendTick>? dividendHistory = security.DividendHistory;
Assert.Equal(new LocalDate(2003, 2, 19), dividendHistory![0].Date);
Assert.Equal(0.08, dividendHistory[0].Dividend);

IReadOnlyList<SplitTick>? splitHistory = security.SplitHistory;
Assert.Equal(new LocalDate(2003, 2, 18), splitHistory![0].Date);
Assert.Equal(1, splitHistory[0].BeforeSplit);
Assert.Equal(2, splitHistory[0].AfterSplit);
```
### currency rates
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder()
    .WithPriceHistory()
    .HistoryStart(Instant.FromUtc(2020, 1, 1, 0, 0))
    .Build();

Security security = await yahooQuotes.GetAsync("EURJPY=X") ?? throw new ArgumentException();

Assert.Equal("EUR/JPY", security.ShortName);

PriceTick tick = security.PriceHistory?[0] ?? throw new ArgumentException();
Assert.Equal(121.970001, tick.Close);

ZonedDateTime zdt = tick.Date;
Assert.Equal(new LocalDate(2020, 1, 1), zdt.Date);
Assert.Equal(new LocalTime(16, 0, 0), zdt.TimeOfDay);
Assert.Equal("Europe/London", zdt.Zone.Id);
```
### history in base currency
```csharp
var security = await new YahooQuotesBuilder()
    .WithPriceHistory(baseCurrency: "JPY")
    .HistoryStart(Instant.FromUtc(2020, 1, 1, 0, 0))
    .Build()
    .GetAsync("TSLA")
    ?? throw new ArgumentException("Unknown symbol: TSLA.");

Assert.Equal("Tesla, Inc.", security.ShortName);
Assert.Equal("USD", security.Currency);

Assert.Equal(46759.61698027081, security.PriceHistory!.First().Close);
```

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
#### Quote Snapshots
```csharp
YahooSnapshot Snapshot = new YahooSnapshot();

Dictionary<string, Security?> securities = 
    await new YahooSnapshot().GetAsync(new[] { "C", "IBM" });

Security? security = securities["IBM"];

Assert.True(security.RegularMarketPrice > 100);
Assert.NotNull(security.LongName);
```
#### Price History
```csharp
YahooHistory History = new YahooHistory();

List<PriceTick>? prices = await History.FromDays(90).GetPricesAsync("IBM");

Assert.True(prices[0].Close > 10);
```
#### Dividend History
```csharp
YahooHistory History = new YahooHistory();

List<DividendTick>? dividends = await History.GetDividendsAsync("IBM");

Assert.True(dividends[0].Dividend > 0);
```
#### Split History
```csharp
YahooHistory History = new YahooHistory();

List<SplitTick>? splits = await History.GetSplitsAsync("IBM");

Assert.True(splits[0].BeforeSplit < splits[0].AfterSplit);
```
#### Currency Rate History (https://www.bankofengland.co.uk)
```csharp
CurrencyHistory CurrencyHistory = new CurrencyHistory();

string currency     = "EUR";
string baseCurrency = "USD";

List<RateTick>? rates = await CurrencyHistory
    .FromDate(new LocalDate(2010,1,1))
    .GetRatesAsync(currency, baseCurrency);

Assert.True(rates[0].Rate > 0);
```

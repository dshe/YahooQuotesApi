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

Security? security = await Snapshot.GetAsync("C");

if (security == null)
    throw new Exception("Invalid Symbol: C");

Assert.True(security.RegularMarketVolume > 0);
```
#### Price History
```csharp
YahooHistory History = new YahooHistory();

List<PriceTick>? ticks = await History.Period(90).GetPricesAsync("C");

if (ticks == null)
    throw new Exception("Invalid symbol: C");

Assert.NotEmpty(ticks);
```
#### Dividend History
```csharp
YahooHistory History = new YahooHistory();

List<DividendTick>? dividends = await History.GetDividendsAsync("AAPL");

if (dividends == null)
    throw new Exception("Invalid symbol: AAPL");

Assert.NotEmpty(dividends);
```
#### Split History
```csharp
YahooHistory History = new YahooHistory();

List<SplitTick>? splits = await History.GetSplitsAsync("AAPL");

if (splits == null)
    throw new Exception("Invalid symbol: AAPL");

Assert.NotEmpty(splits);
```
#### Currency Rate History (https://www.bankofengland.co.uk)
```csharp
CurrencyHistory CurrencyHistory = new CurrencyHistory();

List<RateTick>? rates = await CurrencyHistory
    .Period(100)
    .GetRatesAsync("EURJPY=X");

if (rates == null)
    throw new Exception("Invalid symbol: EURJPY=X");

Assert.NotEmpty(rates);    
```

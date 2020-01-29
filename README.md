## YahooQuotesApi&nbsp;&nbsp; [![Build status](https://ci.appveyor.com/api/projects/status/qx83p28cdqvcpbhm?svg=true)](https://ci.appveyor.com/project/dshe/yahooquotesapi) [![NuGet](https://img.shields.io/nuget/vpre/YahooQuotesApi.svg)](https://www.nuget.org/packages/YahooQuotesApi/) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Yahoo Finance API to retrieve quote snapshots, and quote, dividend and split history**
- asynchronous
- supports **.NET Standard 2.0**
- dependencies: NodaTime, Flurl, CsvHelper
- simple and intuitive API
- tested
```csharp
using YahooQuotesApi;
using NodaTime;
```
#### Quote Snapshots
```csharp
YahooSnapshot Snapshot = new YahooSnapshot();

Dictionary<string, Security?> securities = await Snapshot.GetAsync(new[] { "C", "MSFT" });

Security? msft = securities["MSFT"];
if (msft == null)
    throw new NullReferenceException("Invalid Symbol: MSFT");
Assert.True(msft.RegularMarketVolume > 0);
```
#### Quote History
```csharp
YahooHistory History = new YahooHistory();

Dictionary<string, List<HistoryTick>?> tickLists = await History
  .GetHistoryAsync(new[] { "C", "MSFT" });

List<HistoryTick>? tickList = tickLists["C"];
if (tickList == null)
    throw new Exception("Invalid symbol: C");
Assert.True(tickList[0].Close > 0);
```
#### Dividend History
```csharp
YahooHistory History = new YahooHistory();

List<DividendTick>? dividends = await History
    .Period("America/New_York".ToDateTimeZone(), new LocalDate(2016, 2, 4), new LocalDate(2016, 2, 5))
    .GetDividendsAsync("AAPL");

Assert.Equal(0.52m, dividends[0].Dividend);
```
#### Split History
```csharp
YahooHistory History = new YahooHistory();

List<SplitTick>? splits = await History
    .Period("America/New_York".ToDateTimeZone(), new LocalDate(2014, 6, 8), new LocalDate(2014, 6, 10))
    .GetSplitsAsync("AAPL");
    
Assert.Equal(1, splits[0].BeforeSplit);
Assert.Equal(7, splits[0].AfterSplit);
```

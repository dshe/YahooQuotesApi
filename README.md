# YahooQuotesApi&nbsp;&nbsp; [![Build status](https://ci.appveyor.com/api/projects/status/qx83p28cdqvcpbhm?svg=true)](https://ci.appveyor.com/project/dshe/yahooquotesapi) [![NuGet](https://img.shields.io/nuget/vpre/YahooQuotesApi.svg)](https://www.nuget.org/packages/YahooQuotesApi/) [![NuGet](https://img.shields.io/nuget/dt/YahooQuotesApi?color=orange)](https://www.nuget.org/packages/YahooQuotesApi/) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![Ukraine](https://raw.githubusercontent.com/vshymanskyy/StandWithUkraine/main/badges/StandWithUkraine.svg)](https://stand-with-ukraine.pp.ua)


**Retrieves from Yahoo Finance: quote snapshots, historical quotes, dividends, splits, and modules**
- **.NET 8.0** library
- intellisense support for most properties
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

Snapshot? snapshot = await yahooQuotes.GetSnapshotAsync(symbol);
if (snapshot is null)
    throw new ArgumentException("Unknown symbol.");

Assert.Equal("Apple Inc.", snapshot.LongName);
Assert.True(snapshot.RegularMarketPrice > 0);
```

#### snapshots
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder().Build();

Dictionary<string, Snapshot?> snapshots = await yahooQuotes.GetSnapshotAsync(["AAPL", "BP.L", "USDJPY=X"]);

Snapshot snapshot = snapshots["BP.L"] ?? throw new ArgumentException("Unknown symbol.");

Assert.Equal("BP p.l.c.", snapshot.LongName);
Assert.Equal("GBP=X", snapshot.Currency.Name);
Assert.True(snapshot.RegularMarketPrice > 0);
```

#### price history
```csharp
YahooQuotes yahooQuotes = new YahooQuotesBuilder()
    .WithHistoryStartDate(Instant.FromUtc(2024, 10, 1, 0, 0))
    .Build();

Result<History> result = await yahooQuotes.GetHistoryAsync("MSFT");
History history = result.Value;

Assert.Equal("Microsoft Corporation", history.LongName); // static type. AstraZeneca PLC", history.LongName);
Assert.Equal("USD=X", history.Currency.Name);
Assert.Equal("America/New_York", history.ExchangeTimezoneName);
DateTimeZone tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(history.ExchangeTimezoneName) ??
    throw new ArgumentNullException("Unknown timezone");

ImmutableArray<Tick> ticks = history.Ticks;
Tick firstTick = ticks[0];
ZonedDateTime zdt = firstTick.Date.InZone(tz);
// Note that tick time is market open of 9:30.
Assert.Equal(new LocalDateTime(2024, 10, 1, 9, 30, 0), zdt.LocalDateTime);
Assert.Equal(420.69, firstTick.Close, 2); // In USD
```

#### price history in base currency
```csharp
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
Assert.Equal(820.49, firstBaseTick.Price, 2); // in EUR
```

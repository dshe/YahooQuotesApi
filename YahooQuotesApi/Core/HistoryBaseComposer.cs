using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YahooQuotesApi;

public class HistoryBaseComposer
{
    private readonly IClock Clock;
    private readonly ILogger Logger;
    private readonly bool UseNonAdjustedClose;

    public HistoryBaseComposer(YahooQuotesBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        Clock = builder.Clock;
        Logger = builder.Logger;
        UseNonAdjustedClose = builder.NonAdjustedClose;
    }

    internal void ComposeSecurities(HashSet<Symbol> symbols, Symbol baseSymbol, Dictionary<Symbol, Security?> securities)
    {
        Logger.LogTrace("HistoryBaseComposer");

        foreach (Security security in securities.Values.NotNull())
            security.PriceHistoryBase = GetPriceHistoryBase(security);

        if (baseSymbol == default)
            return;

        List<(Security, Result<ValueTick[]>)> results = new();

        foreach (Symbol symbol in symbols)
        {
            Logger.LogTrace("Symbol: {Symbol}", symbol);

            // currencies
            if (!securities.TryGetValue(symbol, out Security? security))
            {
                if (!symbol.IsCurrency)
                    throw new InvalidOperationException("IsCurrency");
                security = new Security(symbol);
                securities.Add(symbol, security);
                Logger.LogTrace(" added currency");
            }

            if (security is null) // unknown symbol
            {
                Logger.LogTrace(" is unknown");
                continue;
            }

            Result<ValueTick[]> historyBase = ComposeSnap(symbol, baseSymbol, securities);
            results.Add((security, historyBase));
        }

        foreach ((Security security, Result<ValueTick[]> result) in results)
            security.PriceHistoryBase = result;
    }

    private Result<ValueTick[]> GetPriceHistoryBase(Security security)
    {
        Result<PriceTick[]> priceHistory = security.PriceHistory;

        if (priceHistory.HasError)
            return Result<ValueTick[]>.Fail(priceHistory.Error);
        if (!priceHistory.Value.Any())
            return Result<ValueTick[]>.Fail("No history available.");
        if (security.ExchangeTimezone is null)
            return Result<ValueTick[]>.Fail("ExchangeTimezone not found.");
        if (security.ExchangeCloseTime == default)
            return Result<ValueTick[]>.Fail("ExchangeCloseTime not found.");

        List<ValueTick> ticks = priceHistory.Value.Select(priceTick => new ValueTick(
            priceTick.Date.At(security.ExchangeCloseTime).InZoneLeniently(security.ExchangeTimezone!).ToInstant(),
            UseNonAdjustedClose ? priceTick.Close : priceTick.AdjustedClose,
            priceTick.Volume
        )).ToList();

        AddLatest(security, ticks);

        return ticks.ToArray().ToResult();
    }

    private void AddLatest(Security security, List<ValueTick> ticks)
    {
        if (security.RegularMarketPrice is null)
        {
            Logger.LogDebug("RegularMarketPrice unavailable for symbol: {Symbol}.", security.Symbol);
            return;
        }

        if (security.RegularMarketTime == default)
        {
            Logger.LogDebug("RegularMarketTime unavailable for symbol: {Symbol}.", security.Symbol);
            return;
        }

        Instant now = Clock.GetCurrentInstant();
        Instant snapTime = security.RegularMarketTime.ToInstant();
        if (snapTime > now)
        {
            if ((snapTime - now) > Duration.FromSeconds(20))
                Logger.LogWarning("Snapshot date: {SnapTimeInstant} which follows current date: {Now} adjusted for symbol: {Symbol}.", snapTime, now, security.Symbol);
            snapTime = now;
        }

        // assume snaptime is correct
        while (ticks.Any())
        {
            ValueTick lastHistory = ticks.Last()!;
            Instant lastDate = lastHistory.Date;
            if (snapTime > lastDate)
                break;
            // if history already includes snapshot, or exchange closes early
            // hist < snap <= now
            Logger.LogTrace("History tick with date: {Date} equals or follows snapshot date: {SnapTimeInstant} removed for symbol: {Symbol}.", lastDate, snapTime, security.Symbol);
            ticks.Remove(lastHistory);
        }

        ticks.Add(new ValueTick(
            snapTime,
            Convert.ToDouble(security.RegularMarketPrice.Value, CultureInfo.InvariantCulture),
            security.RegularMarketVolume ?? 0
        ));
    }

    private Result<ValueTick[]> ComposeSnap(Symbol symbol, Symbol baseSymbol, Dictionary<Symbol, Security?> securities)
    {
        ValueTick[]? stockTicks = null, currencyTicks = null, baseStockTicks = null, baseCurrencyTicks = null;

        Symbol currency = symbol;
        if (symbol.IsStock)
        {
            Logger.LogTrace(" is stock");
            if (!securities.TryGetValue(symbol, out Security? stockSecurity) || stockSecurity is null)
                throw new InvalidOperationException(nameof(stockSecurity));
            Result<ValueTick[]> res = stockSecurity.PriceHistoryBase;
            if (res.HasError)
                return res;
            if (res.Value.Length < 2)
                return Result<ValueTick[]>.Fail($"Not enough history items: ({res.Value.Length}).");
            stockTicks = res.Value;

            string c = stockSecurity.Currency;
            if (string.IsNullOrEmpty(c))
                return Result<ValueTick[]>.Fail($"Security currency symbol not available.");
            if (!Symbol.TryCreate(c + "=X", out currency))
                return Result<ValueTick[]>.Fail($"Invalid security currency symbol format: '{c}'.");
        }

        if (currency.Currency != "USD")
        {
            if (!Symbol.TryCreate("USD" + currency, out Symbol currencyRate) || !currencyRate.IsCurrencyRate)
                return Result<ValueTick[]>.Fail($"Invalid security currency rate symbol format: '{currencyRate}'.");
            if (!securities.TryGetValue(currencyRate, out Security? currencySecurity))
                throw new InvalidOperationException(nameof(currencySecurity));
            if (currencySecurity is null)
                return Result<ValueTick[]>.Fail($"Currency rate not available: '{currencyRate}'.");
            Result<ValueTick[]> res = currencySecurity.PriceHistoryBase;
            if (res.HasError)
                return res;
            if (res.Value.Length < 2)
                return Result<ValueTick[]>.Fail($"Currency rate not enough history items({res.Value.Length}): '{currencyRate}'.");
            currencyTicks = res.Value;
        }

        Symbol baseCurrency = baseSymbol;
        if (baseSymbol.IsStock)
        {
            if (!securities.TryGetValue(baseSymbol, out Security? baseStockSecurity))
                throw new InvalidOperationException(nameof(baseStockSecurity));
            if (baseStockSecurity is null)
                return Result<ValueTick[]>.Fail($"Base stock security not available: '{baseSymbol}'.");
            Result<ValueTick[]> res = baseStockSecurity.PriceHistoryBase;
            if (res.HasError)
                return res;
            if (res.Value.Length < 2)
                return Result<ValueTick[]>.Fail($"Base stock security not enough history items({res.Value.Length}): '{baseSymbol}'.");
            baseStockTicks = res.Value;

            string c = baseStockSecurity.Currency;
            if (string.IsNullOrEmpty(c))
                return Result<ValueTick[]>.Fail($"Base security currency symbol not available.");
            if (!Symbol.TryCreate(c + "=X", out baseCurrency))
                return Result<ValueTick[]>.Fail($"Invalid base security currency symbol: '{c}'.");
        }

        if (baseCurrency.Currency != "USD")
        {
            if (!Symbol.TryCreate("USD" + baseCurrency, out Symbol currencyRate) || !currencyRate.IsCurrencyRate)
                return Result<ValueTick[]>.Fail($"Invalid base currency rate symbol: '{currencyRate}'.");
            if (!securities.TryGetValue(currencyRate, out Security? baseCurrencySecurity))
                throw new InvalidOperationException(nameof(baseCurrencySecurity));
            if (baseCurrencySecurity is null)
                return Result<ValueTick[]>.Fail($"Base currency rate not available: '{currencyRate}'.");
            Result<ValueTick[]> res = baseCurrencySecurity.PriceHistoryBase;
            if (res.HasError)
                return res;
            if (res.Value.Length < 2)
                return Result<ValueTick[]>.Fail($"Base currency rate not enough history items ({res.Value.Length}): '{baseSymbol}'.");
            baseCurrencyTicks = res.Value;
        }

        ValueTick[]? dateTicks = stockTicks ?? currencyTicks ?? baseStockTicks ?? baseCurrencyTicks;
        if (dateTicks is null)
            return Result<ValueTick[]>.Fail("No history ticks found.");

        return dateTicks
            .Select(tick => tick.Date)
            .Select(date => (date, rate: GetRate(date)))
            .Where(x => !double.IsNaN(x.rate))
            .Select(x => new ValueTick(x.date, x.rate, 0))
            .ToArray()
            .ToResult();

        // local function
        double GetRate(Instant date) => 1d
                      .MultiplyByValue(date, stockTicks)
                      .DivideByValue(date, currencyTicks)
                      .MultiplyByValue(date, baseCurrencyTicks)
                      .DivideByValue(date, baseStockTicks);
    }
}

internal static class HistoryBaseComposerExtensions
{
    internal static double MultiplyByValue(this double value, Instant date, ValueTick[]? ticks)
    {
        if (ticks is not null && ticks.Any())
            value *= ticks.InterpolateValue(date);
        return value;
    }

    internal static double DivideByValue(this double value, Instant date, ValueTick[]? ticks)
    {
        if (ticks is not null && ticks.Any())
            value /= ticks.InterpolateValue(date);
        return value;
    }
}

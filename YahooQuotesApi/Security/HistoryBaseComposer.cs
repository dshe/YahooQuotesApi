using System.Collections.Generic;
using System.Linq;
namespace YahooQuotesApi;

internal static class HistoryBaseComposer
{
    internal static void Compose(HashSet<Symbol> symbols, Symbol baseSymbol, Dictionary<Symbol, Security?> securities)
    {
        foreach (Symbol symbol in symbols)
        {
            if (!securities.TryGetValue(symbol, out Security? security))
            {
                if (!symbol.IsCurrency)
                    throw new InvalidOperationException("IsCurrency");
                security = new Security(symbol);
                securities.Add(symbol, security);
            }

            if (security is null) // unknown symbol
                continue;

            Result<ValueTick[]> historyBase = ComposeSnap(symbol, baseSymbol, securities);
            security.PriceHistoryBase = historyBase;
        }
    }

    private static Result<ValueTick[]> ComposeSnap(Symbol symbol, Symbol baseSymbol, Dictionary<Symbol, Security?> securities)
    {
        ValueTick[]? stockTicks = null, currencyTicks = null, baseStockTicks = null, baseCurrencyTicks = null;

        Symbol? currency = symbol;
        if (symbol.IsStock)
        {
            if (!securities.TryGetValue(symbol, out Security? stockSecurity) || stockSecurity is null)
                throw new InvalidOperationException(nameof(stockSecurity));
            Result<ValueTick[]> res = stockSecurity.PriceHistoryBase;
            if (res.HasError)
                return res;
            if (res.Value.Length < 2)
                return Result<ValueTick[]>.Fail($"Not enough history items({res.Value.Length}).");
            stockTicks = res.Value;

            string c = stockSecurity.Currency;
            if (string.IsNullOrEmpty(c))
                return Result<ValueTick[]>.Fail($"Security currency symbol not available.");

            currency = Symbol.TryCreate(c + "=X");
            if (currency is null)
                return Result<ValueTick[]>.Fail($"Invalid security currency symbol format: '{c}'.");
        }
        if (currency.Currency != "USD")
        {
            Symbol? currencyRate = Symbol.TryCreate("USD" + currency);
            if (currencyRate is null || !currencyRate.IsCurrencyRate)
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

        Symbol? baseCurrency = baseSymbol;
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
            baseCurrency = Symbol.TryCreate(c + "=X");
            if (baseCurrency is null)
                return Result<ValueTick[]>.Fail($"Invalid base security currency symbol: '{c}'.");
        }
        if (baseCurrency.Currency != "USD")
        {
            Symbol? currencyRate = Symbol.TryCreate("USD" + baseCurrency);
            if (currencyRate is null || !currencyRate.IsCurrencyRate)
                return Result<ValueTick[]>.Fail($"Invalid base currency rate symbol: '{currencyRate}'.");

            if (!securities.TryGetValue(currencyRate, out Security? baseCurrencySecurity))
                throw new InvalidOperationException(nameof(baseCurrencySecurity));
            if (baseCurrencySecurity is null)
                return Result<ValueTick[]>.Fail($"Base currency rate not available: '{currencyRate}'.");
            Result<ValueTick[]> res = baseCurrencySecurity.PriceHistoryBase;
            if (res.HasError)
                return res;
            if (res.Value.Length < 2)
                return Result<ValueTick[]>.Fail($"Base currency rate not enough history items({res.Value.Length}): '{baseSymbol}'.");
            baseCurrencyTicks = res.Value;
        }

        ValueTick[]? dateTicks = stockTicks ?? currencyTicks ?? baseStockTicks ?? baseCurrencyTicks;
        if (dateTicks is null)
            return Result<ValueTick[]>.Fail("No history ticks found.");

        return dateTicks
            .Select(tick => tick.Date)
            .Select(date => (date, rate: GetRate(date)))
            .Where(x => !double.IsNaN(x.rate))
            .Select(x => new ValueTick { Date = x.date, Value = x.rate })
            .ToArray()
            .ToResult();

        double GetRate(Instant date) => 1d
                      .MultiplyByPrice(date, stockTicks)
                      .DivideByPrice(date, currencyTicks)
                      .MultiplyByPrice(date, baseCurrencyTicks)
                      .DivideByPrice(date, baseStockTicks);
    }
}

internal static class SnapExtensions
{
    internal static double MultiplyByPrice(this double value, Instant date, ValueTick[]? ticks)
    {
        if (ticks is not null && ticks.Any())
            value *= ticks.InterpolateValue(date);
        return value;
    }

    internal static double DivideByPrice(this double value, Instant date, ValueTick[]? ticks)
    {
        if (ticks is not null && ticks.Any())
            value /= ticks.InterpolateValue(date);
        return value;
    }
}


using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace YahooQuotesApi
{
    internal static class TickListArrayIndex
    {
        internal const int Stock = 0, Currency = 1, BaseCurrency = 2, BaseStock = 3;
    }

    internal static class HistoryBaseComposer
    {
        internal static void Compose(HashSet<Symbol> symbols, Symbol baseSymbol, Dictionary<Symbol, Security?> securities)
        {
            foreach (var symbol in symbols)
            {
                if (!securities.TryGetValue(symbol, out var security))
                {
                    if (!symbol.IsCurrency)
                        throw new InvalidOperationException("IsCurrency");
                    security = new Security(symbol);
                    securities.Add(symbol, security);
                }

                if (security is null) // unknown symbol
                    continue;

                var historyBase = ComposeSnap(symbol, baseSymbol, securities);                    
                security.PriceHistoryBase = historyBase;
            }
        }

        private static Result<ValueTick[]> ComposeSnap(Symbol symbol, Symbol baseSymbol, Dictionary<Symbol, Security?> securities)
        {
            var tickLists = new ValueTick[4][];

            var currency = symbol;
            if (symbol.IsStock)
            {
                if (!securities.TryGetValue(symbol, out var stockSecurity) || stockSecurity is null)
                    throw new InvalidOperationException(nameof(stockSecurity));
                var res = stockSecurity.PriceHistoryBase;
                if (res.HasError)
                    return res;
                if (res.Value.Length < 2)
                    return Result<ValueTick[]>.Fail($"Not enough history items({res.Value.Length}).");
                tickLists[TickListArrayIndex.Stock] = res.Value;

                var c = stockSecurity.Currency;
                if (string.IsNullOrEmpty(c))
                    return Result<ValueTick[]>.Fail($"Security currency symbol not available.");
                currency = Symbol.TryCreate(c + "=X");
                if (currency is null)
                    return Result<ValueTick[]>.Fail($"Invalid security currency symbol format: '{c}'.");
            }
            if (currency.Currency != "USD")
            {
                var currencyRate = Symbol.TryCreate("USD" + currency);
                if (currencyRate is null || !currencyRate.IsCurrencyRate)
                    return Result<ValueTick[]>.Fail($"Invalid security currency rate symbol format: '{currencyRate}'.");
                if (!securities.TryGetValue(currencyRate, out var currencySecurity))
                    throw new InvalidOperationException(nameof(currencySecurity));
                if (currencySecurity is null)
                    return Result<ValueTick[]>.Fail($"Currency rate not available: '{currencyRate}'.");
                var res = currencySecurity.PriceHistoryBase;
                if (res.HasError)
                    return res;
                if (res.Value.Length < 2)
                    return Result<ValueTick[]>.Fail($"Currency rate not enough history items({res.Value.Length}): '{currencyRate}'.");
                tickLists[TickListArrayIndex.Currency] = res.Value;
            }

            var baseCurrency = baseSymbol;
            if (baseSymbol.IsStock)
            {
                if (!securities.TryGetValue(baseSymbol, out var baseStockSecurity))
                    throw new InvalidOperationException(nameof(baseStockSecurity));
                if (baseStockSecurity is null)
                    return Result<ValueTick[]>.Fail($"Base stock security not available: '{baseSymbol}'.");
                var res = baseStockSecurity.PriceHistoryBase;
                if (res.HasError)
                    return res;
                if (res.Value.Length < 2)
                    return Result<ValueTick[]>.Fail($"Base stock security not enough history items({res.Value.Length}): '{baseSymbol}'.");
                tickLists[TickListArrayIndex.BaseStock] = res.Value;

                var c = baseStockSecurity.Currency;
                if (string.IsNullOrEmpty(c))
                    return Result<ValueTick[]>.Fail($"Base security currency symbol not available.");
                baseCurrency = Symbol.TryCreate(c + "=X");
                if (baseCurrency is null)
                    return Result<ValueTick[]>.Fail($"Invalid base security currency symbol: '{c}'.");
            }
            if (baseCurrency.Currency != "USD")
            {
                var currencyRate = Symbol.TryCreate("USD" + baseCurrency);
                if (currencyRate is null || !currencyRate.IsCurrencyRate)
                    return Result<ValueTick[]>.Fail($"Invalid base currency rate symbol: '{currencyRate}'.");

                if (!securities.TryGetValue(currencyRate, out var baseCurrencySecurity))
                    throw new InvalidOperationException(nameof(baseCurrencySecurity));
                if (baseCurrencySecurity is null)
                    return Result<ValueTick[]>.Fail($"Base currency rate not available: '{currencyRate}'.");
                var res = baseCurrencySecurity.PriceHistoryBase;
                if (res.HasError)
                    return res;
                if (res.Value.Length < 2)
                    return Result<ValueTick[]>.Fail($"Base currency rate not enough history items({res.Value.Length}): '{baseSymbol}'.");
                tickLists[TickListArrayIndex.BaseCurrency] = res.Value;
            }

            var dateTicks = tickLists.FirstOrDefault(a => a != null && a.Any());
            if (dateTicks is null)
                return Result<ValueTick[]>.Fail("No history ticks found.");

            return GetTicks(dateTicks, tickLists)
                .ToArray()
                .ToResult();
        }

        private static List<ValueTick> GetTicks(ValueTick[] dateTicks, ValueTick[][] tickLists)
        {
            return dateTicks
                .Select(tick => tick.Date)
                .Select(date => (date, rate: GetRate(date)))
                .Where(x => !double.IsNaN(x.rate))
                .Select(x => new ValueTick { Date = x.date, Value = x.rate })
                .ToList();

            double GetRate(Instant date) => 1d
                .MultiplyByPrice(date, TickListArrayIndex.Stock, tickLists)
                .DivideByPrice(date, TickListArrayIndex.Currency, tickLists)
                .MultiplyByPrice(date, TickListArrayIndex.BaseCurrency, tickLists)
                .DivideByPrice(date, TickListArrayIndex.BaseStock, tickLists);
        }
    }

    internal static class SnapExtensions
    {
        internal static double MultiplyByPrice(this double value, Instant date, int index, ValueTick[][] tickLists)
        {
            var ticks = tickLists[index];
            if (ticks != null && ticks.Any())
                value *= ticks.InterpolateClose(date);
            return value;
        }
        internal static double DivideByPrice(this double value, Instant date, int index, ValueTick[][] tickLists)
        {
            var ticks = tickLists[index];
            if (ticks != null && ticks.Any())
                value /= ticks.InterpolateClose(date);
            return value;
        }
    }
}


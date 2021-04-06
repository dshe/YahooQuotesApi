using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace YahooQuotesApi
{
    internal enum TickListType
    {
        Stock = 0, Currency, BaseCurrency, BaseStock
    }

    internal static class HistoryBaseComposer
    {
        internal static void Compose(List<Symbol> symbols, Symbol baseSymbol, Dictionary<Symbol, Security?> securities)
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

                if (security == null) // invalid snapshot symbol
                    continue;

                var historyBase = ComposeSnap(symbol, baseSymbol, securities);                    
                security.PriceHistoryBase = historyBase;
            }
        }

        private static Result<PriceTick[]> ComposeSnap(Symbol symbol, Symbol baseSymbol, Dictionary<Symbol, Security?> securities)
        {
            var tickLists = new PriceTick[4][];

            var currency = symbol;
            if (symbol.IsStock)
            {
                if (!securities.TryGetValue(symbol, out var stockSecurity))
                    throw new InvalidOperationException(nameof(stockSecurity));
                if (stockSecurity == null)
                    throw new Exception("stockSecurity");
                var res = stockSecurity.PriceHistoryBase;
                if (res.HasError)
                    return res;
                if (res.Value.Length < 2)
                    return Result<PriceTick[]>.Fail($"Not enough history items({res.Value.Length}).");
                tickLists[(int)TickListType.Stock] = res.Value;

                var c = stockSecurity.Currency;
                if (string.IsNullOrEmpty(c))
                    return Result<PriceTick[]>.Fail($"Security currency symbol not available.");
                currency = Symbol.TryCreate(c + "=X");
                if (currency == null)
                    return Result<PriceTick[]>.Fail($"Invalid security currency symbol format: '{c}'.");
            }
            if (currency.Currency != "USD")
            {
                var currencyRate = Symbol.TryCreate("USD" + currency);
                if (currencyRate == null || !currencyRate.IsCurrencyRate)
                    return Result<PriceTick[]>.Fail($"Invalid security currency rate symbol format: '{currencyRate}'.");
                if (!securities.TryGetValue(currencyRate, out var currencySecurity))
                    throw new InvalidOperationException(nameof(currencySecurity));
                if (currencySecurity == null)
                    return Result<PriceTick[]>.Fail($"Currency rate not available: '{currencyRate}'.");
                var res = currencySecurity.PriceHistoryBase;
                if (res.HasError)
                    return res;
                if (res.Value.Length < 2)
                    return Result<PriceTick[]>.Fail($"Currency rate not enough history items({res.Value.Length}): '{currencyRate}'.");
                tickLists[(int)TickListType.Currency] = res.Value;
            }

            var baseCurrency = baseSymbol;
            if (baseSymbol.IsStock)
            {
                if (!securities.TryGetValue(baseSymbol, out var baseStockSecurity))
                    throw new InvalidOperationException(nameof(baseStockSecurity));
                if (baseStockSecurity == null)
                    return Result<PriceTick[]>.Fail($"Base stock security not available: '{baseSymbol}'.");
                var res = baseStockSecurity.PriceHistoryBase;
                if (res.HasError)
                    return res;
                if (res.Value.Length < 2)
                    return Result<PriceTick[]>.Fail($"Base stock security not enough history items({res.Value.Length}): '{baseSymbol}'.");
                tickLists[(int)TickListType.BaseStock] = res.Value;

                var c = baseStockSecurity.Currency;
                if (string.IsNullOrEmpty(c))
                    return Result<PriceTick[]>.Fail($"Base security currency symbol not available.");
                baseCurrency = Symbol.TryCreate(c + "=X");
                if (baseCurrency == null)
                    return Result<PriceTick[]>.Fail($"Invalid base security currency symbol: '{c}'.");
            }
            if (baseCurrency.Currency != "USD")
            {
                var currencyRate = Symbol.TryCreate("USD" + baseCurrency);
                if (currencyRate == null || !currencyRate.IsCurrencyRate)
                    return Result<PriceTick[]>.Fail($"Invalid base currency rate symbol: '{currencyRate}'.");

                if (!securities.TryGetValue(currencyRate, out var baseCurrencySecurity))
                    throw new InvalidOperationException(nameof(baseCurrencySecurity));
                if (baseCurrencySecurity == null)
                    return Result<PriceTick[]>.Fail($"Base currency rate not available: '{currencyRate}'.");
                var res = baseCurrencySecurity.PriceHistoryBase;
                if (res.HasError)
                    return res;
                if (res.Value.Length < 2)
                    return Result<PriceTick[]>.Fail($"Base currency rate not enough history items({res.Value.Length}): '{baseSymbol}'.");
                tickLists[(int)TickListType.BaseCurrency] = res.Value;
            }

            var dateTicks = tickLists.FirstOrDefault(a => a != null && a.Any());
            if (dateTicks == null)
                return Result<PriceTick[]>.Fail("No history ticks found.");

            return GetTicks(dateTicks, tickLists)
                .ToArray()
                .ToResult();
        }

        private static List<PriceTick> GetTicks(PriceTick[] dateTicks, PriceTick[][] tickLists)
        {
            return dateTicks
                .Select(tick => tick.Date)
                .Select(date => (date, rate: GetRate(date)))
                .Where(x => !double.IsNaN(x.rate))
                .Select(x => new PriceTick(x.date, x.rate))
                .ToList();

            double GetRate(ZonedDateTime date) => 1d
                .MultiplyByPrice(date, TickListType.Stock, tickLists)
                .DivideByPrice(date, TickListType.Currency, tickLists)
                .MultiplyByPrice(date, TickListType.BaseCurrency, tickLists)
                .DivideByPrice(date, TickListType.BaseStock, tickLists);
        }
    }

    internal static class SnapExtensions
    {
        internal static double MultiplyByPrice(this double value, ZonedDateTime date, TickListType type, PriceTick[][] tickLists)
        {
            var ticks = tickLists[(int)type];
            if (ticks != null && ticks.Any())
                value *= ticks.InterpolateClose(date);
            return value;
        }
        internal static double DivideByPrice(this double value, ZonedDateTime date, TickListType type, PriceTick[][] tickLists)
        {
            var ticks = tickLists[(int)type];
            if (ticks != null && ticks.Any())
                value /= ticks.InterpolateClose(date);
            return value;
        }
    }
}


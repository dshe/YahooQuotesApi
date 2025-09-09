using System.Collections.Immutable;
namespace YahooQuotesApi;

public sealed class HistoryBasePricesCreator
{
    private IClock Clock { get; }
    private ILogger Logger { get; }
    private bool UseAdjustedClose { get; }
    public HistoryBasePricesCreator(IClock clock, ILogger logger, YahooQuotesBuilder builder)
    {
        Clock = clock;
        Logger = logger;
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        UseAdjustedClose = builder.UseAdjustedClose;
    }

    internal Dictionary<Symbol, Result<History>> Create(HashSet<Symbol> symbols, Symbol baseSymbol, Dictionary<Symbol, Result<History>> results)
    {
        foreach (History history in results.Values.Where(r => r.HasValue).Select(r => r.Value))
            SetBasePrices(history);

        if (baseSymbol == default)
            return results;

        (Symbol symbol, Result<History> result, BaseTick[]? baseTick)[] res =
            [.. symbols.Select(symbol => ComposeBasePrices(symbol, baseSymbol, results))];

        Dictionary<Symbol, Result<History>> dict = new(symbols.Count);
        foreach (var (symbol, result, baseTick) in res)
        {
            if (result.HasValue && baseTick is not null)
                result.Value.BaseTicks = baseTick.AsImmutableArray();
            dict.Add(symbol, result);
        }
        return dict;
    }

    private void SetBasePrices(History history)
    {
        Duration marketDuration = Duration.Zero;
        if (history.Symbol.IsStock)
        {
            // Price.Date is the START of trading. marketDuration 
            // Add trading day. Ok unless half/partial trading day.
            TradingPeriod period = history.CurrentTradingPeriod.Single(x => x.Name == "regular");
            marketDuration = period.EndDate - period.StartDate;
            Logger.LogDebug("Market duration: {MarketDuration}", marketDuration);
        }

        int length = history.Ticks.Length;
        List<BaseTick> baseTicks = new(length);

        for (int i = 0; i < length; i++)
        {
            var tick = history.Ticks[i];
            double val = (UseAdjustedClose && history.Symbol.IsStock) ? tick.AdjustedClose : tick.Close;
            if (val == 0)
                continue;
            if (i < length - 1)
                baseTicks.Add(new BaseTick(tick.Date.Plus(marketDuration), val, tick.Volume));
            else
                baseTicks.Add(new BaseTick(history.RegularMarketTime, (double)history.RegularMarketPrice, history.RegularMarketVolume));
        }
        /*
        string? errorMessage = basePrices.IsIncreasing(x => x.Date);
        if (errorMessage is not null)
        {
            Logger.LogError("Not increasing: {Symbol} {ErrorMessage}", history.Symbol, errorMessage);
            throw new InvalidOperationException("Not increasing.");
        }
        if (history.Prices[^1].Date > Clock.GetCurrentInstant())
            throw new InvalidOperationException("Future date.");
        if (history.RegularMarketTime > Clock.GetCurrentInstant())
            throw new InvalidOperationException("Future date.");
        if (basePrices[^1].Date > Clock.GetCurrentInstant())
            throw new InvalidOperationException("Future date.");
        */
        history.BaseTicks = [.. baseTicks];
    }

    private (Symbol, Result<History>, BaseTick[]?) ComposeBasePrices(Symbol symbol, Symbol baseSymbol, Dictionary<Symbol, Result<History>> results)
    {
        Result<History> symbolResult = results[symbol];
        if (symbolResult.HasError)
            return (symbol, symbolResult, null);

        if (symbol == baseSymbol)
        {
            BaseTick[] bts = [ new(Instant.MinValue, 1, 0), new(Instant.MaxValue, 1, 0)];
            symbolResult.Value.BaseTicks = bts.AsImmutableArray();
            return (symbol, symbolResult, bts);
        }

        History? symbolHistory = null, symbolCurrencyHistory = null, baseSymbolHistory = null, baseSymbolCurrencyHistory = null;
        BaseTick[]? symbolTicks = null, symbolCurrencyTicks = null, baseSymbolTicks = null, baseSymbolCurrencyTicks = null;

        Symbol currency = symbol;
        if (symbol.IsStock)
        {
            Logger.LogTrace(" is stock");
            symbolHistory = symbolResult.Value;
            symbolTicks = symbolHistory.BaseTicks.AsArray();
            currency = symbolHistory.Currency;
            if (!currency.IsValid)
            {
                Result<History> errorResult = Result<History>.Fail($"{symbol}: currency not indicated.");
                results[symbol] = errorResult;
                return (symbol, errorResult, null); 
            }
        }
        if (currency.Currency != "USD")
        {
            if (!Symbol.TryCreate("USD" + currency, out Symbol currencyRate) || !currencyRate.IsCurrencyRate)
                throw new InvalidOperationException($"Invalid history currency rate symbol format: '{currencyRate}'.");
            Result<History> result = results[currencyRate];
            if (result.HasError)
            {
                Result<History> errorResult = Result<History>.Fail(result.Error);
                results[symbol] = errorResult;
                return (symbol, errorResult, null);
            }
            symbolCurrencyHistory = result.Value;
            symbolCurrencyTicks = symbolCurrencyHistory.BaseTicks.AsArray();
            if (symbolCurrencyTicks.Length < 2)
                throw new InvalidOperationException($"Currency rate not enough history items({symbolCurrencyTicks.Length}): '{currencyRate}'.");
        }

        Symbol baseCurrency = baseSymbol;
        if (baseSymbol.IsStock)
        {
            Result<History> result = results[baseSymbol];
            if (result.HasError)
            {
                Result<History> errorResult = Result<History>.Fail(result.Error);
                results[symbol] = errorResult;
                return (symbol, errorResult, null);
            }
            baseSymbolHistory = result.Value;
            baseSymbolTicks = baseSymbolHistory.BaseTicks.AsArray();
            baseCurrency = baseSymbolHistory.Currency;
            if (!baseCurrency.IsValid)
            {
                Result<History> errorResult = Result<History>.Fail($"{baseSymbol}: currency not indicated.");
                results[baseSymbol] = errorResult;
                return (baseSymbol, errorResult, null);
            }
        }

        if (baseCurrency.Currency != "USD")
        {
            if (!Symbol.TryCreate("USD" + baseCurrency, out Symbol currencyRate) || !currencyRate.IsCurrencyRate)
                throw new InvalidOperationException($"Invalid base currency rate symbol: '{currencyRate}'.");
            Result<History> result = results[currencyRate];
            if (result.HasError)
            {
                Result<History> errorResult = Result<History>.Fail(result.Error);
                results[symbol] = errorResult;
                return (symbol, errorResult, null);
            }
            baseSymbolCurrencyHistory = result.Value;
            baseSymbolCurrencyTicks = baseSymbolCurrencyHistory.BaseTicks.AsArray();
            if (baseSymbolCurrencyTicks.Length < 2)
                throw new InvalidOperationException($"Base currency rate not enough history items ({baseSymbolCurrencyTicks.Length}): '{baseSymbol}'.");
        }

        if (symbol.IsCurrency)
        {
            if (baseSymbol.IsCurrency)
                symbolResult.Value.Currency = baseSymbol;
            History? h = symbolCurrencyHistory ?? baseSymbolCurrencyHistory ?? symbolHistory ?? baseSymbolHistory;
            symbolResult.Value.ExchangeTimezoneName = h!.ExchangeTimezoneName;
        }

        BaseTick[]? dateTicks = symbolTicks ?? baseSymbolTicks ?? symbolCurrencyTicks ?? baseSymbolCurrencyTicks;
        if (dateTicks is null || dateTicks.Length == 0)
            throw new InvalidOperationException("No history.");

        List<BaseTick> baseTicks = new(dateTicks.Length);
        foreach (BaseTick baseTick in dateTicks)
        {
            double rate = GetRate(baseTick.Date);
            if (double.IsNaN(rate))
                continue;
            baseTicks.Add(new BaseTick(baseTick.Date, rate, GetVolume(baseTick.Date)));
        }

        return (symbol, symbolResult, baseTicks.ToArray());

        // local functions
        double GetRate(Instant date) => 1d
                      .MultiplyByValue(date, symbolTicks)
                      .DivideByValue(date, symbolCurrencyTicks)
                      .MultiplyByValue(date, baseSymbolCurrencyTicks)
                      .DivideByValue(date, baseSymbolTicks);

        long GetVolume(Instant date)
        {
            if (symbolTicks is not null && symbolTicks.Length != 0)
                return symbolTicks.InterpolateVolume(date);
            return 0;
        }
    }
}

file static class HistoryBaseComposerExtensions
{
    internal static double MultiplyByValue(this double value, Instant date, BaseTick[]? ticks)
    {
        if (ticks is not null && ticks.Length != 0)
            value *= ticks.InterpolatePrice(date);
        return value;
    }

    internal static double DivideByValue(this double value, Instant date, BaseTick[]? ticks)
    {
        if (ticks is not null && ticks.Length != 0)
            value /= ticks.InterpolatePrice(date);
        return value;
    }
}

namespace YahooQuotesApi;

public sealed class Quotes
{
    private ILogger Logger { get; }
    private YahooSnapshot Snapshot { get; }
    private YahooHistory History { get; }
    private HistoryBaseComposer HistoryBaseComposer { get; }

    // must be public to support dependency injection
    public Quotes(ILogger logger, YahooSnapshot snapshot, YahooHistory history, HistoryBaseComposer hbc)
    {
        Logger = logger;
        Snapshot = snapshot;
        History = history;
        HistoryBaseComposer = hbc;
    }

    internal async Task<Dictionary<Symbol, Security?>> GetAsync(HashSet<Symbol> symbols, Histories historyFlags, Symbol historyBase, CancellationToken ct)
    {
        if (historyBase == default && symbols.Any(s => s.IsCurrency))
            throw new ArgumentException($"Invalid symbol: {symbols.First(s => s.IsCurrency)}.");
        if (historyBase != default)
        {
            if (!historyFlags.HasFlag(Histories.PriceHistory))
                throw new ArgumentException("PriceHistory must be enabled when historyBase is specified.");
            if (historyBase.IsCurrencyRate)
                throw new ArgumentException($"Invalid base symbol: {historyBase}.");
            if (symbols.Any(s => s.IsCurrencyRate))
                throw new ArgumentException($"Invalid symbol: {symbols.First(s => s.IsCurrencyRate)}.");
        }

        try
        {
            return await GetSecuritiesAsync(symbols, historyFlags, historyBase, ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "YahooQuotes GetAsync() error.");
            throw;
        }
    }

    private async Task<Dictionary<Symbol, Security?>> GetSecuritiesAsync(HashSet<Symbol> symbols, Histories historyFlags, Symbol historyBase, CancellationToken ct)
    {
        HashSet<Symbol> stockAndCurrencyRateSymbols = symbols.Where(s => s.IsStock || s.IsCurrencyRate).ToHashSet();
        if (historyBase != default && historyBase.IsStock)
            stockAndCurrencyRateSymbols.Add(historyBase);
        Dictionary<Symbol, Security?> securities = await Snapshot.GetAsync(stockAndCurrencyRateSymbols, ct).ConfigureAwait(false);

        if (historyFlags == Histories.None)
            return securities;

        if (historyBase != default)
            await AddCurrenciesToSecurities(symbols, historyBase, securities, ct).ConfigureAwait(false);

        await AddHistoryToSecurities(securities, historyFlags, ct).ConfigureAwait(false);

        if (historyFlags.HasFlag(Histories.PriceHistory))
            HistoryBaseComposer.ComposeSecurities(symbols, historyBase, securities);

        return securities;
    }

    private async Task AddCurrenciesToSecurities(HashSet<Symbol> symbols, Symbol historyBase, Dictionary<Symbol, Security?> securities, CancellationToken ct)
    {
        // currency securities + historyBase currency + security currencies
        HashSet<Symbol> currencySymbols = symbols.Where(s => s.IsCurrency).ToHashSet();
        foreach (Security security in securities.Values.NotNull())
        {
            if (Symbol.TryCreate(security.Currency + "=X", out Symbol currencySymbol))
                currencySymbols.Add(currencySymbol);
            else
                security.PriceHistoryBase = Result<ValueTick[]>.Fail($"Invalid currency symbol: '{security.Currency}'.");
        }
        if (historyBase.IsCurrency)
            currencySymbols.Add(historyBase);

        HashSet<Symbol> rateSymbols = currencySymbols
            .Where(c => c.Currency is not "USD")
            .Select(c => $"USD{c.Currency}=X".ToSymbol())
            .ToHashSet();

        if (rateSymbols.Count == 0)
            return;

        Dictionary<Symbol, Security?> currencyRateSecurities = await Snapshot.GetAsync(rateSymbols, ct).ConfigureAwait(false);
        foreach (KeyValuePair<Symbol, Security?> kvp in currencyRateSecurities)
            securities[kvp.Key] = kvp.Value;
    }

    private async Task AddHistoryToSecurities(Dictionary<Symbol, Security?> securities, Histories historyFlags, CancellationToken ct)
    {
        Histories[] histories = Enum.GetValues<Histories>()
            .Where(history => historyFlags.HasFlag(history))
            .ToArray();

        (Security security, Histories flag)[] jobs = securities.Values
            .NotNull()
            .Select(security => histories.Select(history => (security, history)))
            .SelectMany(x => x)
            .ToArray();

        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = 8,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(jobs, parallelOptions, async (job, ct) =>
        {
            Histories flag = job.flag;
            Security security = job.security;
            Symbol symbol = security.Symbol;
            if (flag is Histories.PriceHistory)
                security.PriceHistory = await History.GetTicksAsync<PriceTick>(symbol, ct).ConfigureAwait(false);
            else if (flag is Histories.DividendHistory)
                security.DividendHistory = await History.GetTicksAsync<DividendTick>(symbol, ct).ConfigureAwait(false);
            else if (flag is Histories.SplitHistory)
                security.SplitHistory = await History.GetTicksAsync<SplitTick>(symbol, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}

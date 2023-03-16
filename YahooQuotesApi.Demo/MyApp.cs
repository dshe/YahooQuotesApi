using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YahooQuotesApi.Demo;

public class MyApp
{
    private readonly ILogger Logger;
    private readonly YahooQuotes YahooQuotes;

    public MyApp(ILogger logger)
    {
        Logger = logger;

        Instant start = SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromDays(30));

        YahooQuotes = new YahooQuotesBuilder()
            .WithLogger(Logger)
            .WithHistoryStartDate(start)
            .Build();
    }

    public async Task Run(int number, Histories flags, string baseCurrency)
    {
        List<Symbol> symbols = GetSymbols(number);

        Console.WriteLine($"Loaded {symbols.Count} symbols.");
        Console.WriteLine($"Retrieving values...");

        Stopwatch watch = new();
        watch.Start();
        Dictionary<string, Security?> securities = await YahooQuotes.GetAsync(symbols.Select(x => x.Name), flags, baseCurrency);
        watch.Stop();

        int n = securities.Values.Count(x => x is not null);
        double s = watch.Elapsed.TotalSeconds;
        double rate = n / s;
        Logger.LogWarning("Rate = {N}/{S:N2} = {Rate:N2} Hz", n, s, rate);

        Analyze(securities);
    }

    private List<Symbol> GetSymbols(int number)
    {
        const string path = @"..\..\..\symbols.txt";

        List<string> lines = File
            .ReadAllLines(path)
            .Where(line => !line.StartsWith("#"))
            .Take(number)
            .ToList();

        List<string> errors = lines
            .Where(t => !Symbol.TryCreate(t, out _))
            .ToList();

        if (errors.Any())
            Logger.LogWarning("Invalid symbol names: {names}.", string.Join(", ", errors));

        List<Symbol> symbols = lines
            .Select(t => t.ToSymbol(false))
            .Where(s => s.IsValid)
            .OrderBy(x => x)
            .ToList();

        return symbols;
    }

    private void Analyze(Dictionary<string, Security?> dict)
    {
        Logger.LogWarning("Symbols total: {count}.", dict.Count);
        Logger.LogWarning("Symbols not found: {count}.", dict.Count(x => x.Value is null));

        IEnumerable<KeyValuePair<string, Security>> kvp = dict.Where(kv => kv.Value is not null).Cast<KeyValuePair<string, Security>>();
        List<Security> securities = kvp.Select(kv => kv.Value).ToList();

        Logger.LogWarning("Symbols found: {Count}.", securities.Count);

        Logger.LogWarning("Symbols no currency: {Securities}.", securities.Count(x => x.Currency == ""));
        Logger.LogWarning("Symbols no timezone: {Securities}.", securities.Count(x => x.ExchangeTimezoneName == ""));

        Logger.LogWarning("Symbols with history not set: {Securities}.", securities.Count(x => x.PriceHistory.IsUndefined));
        Logger.LogWarning("Symbols with history found:   {Securities}.", securities.Count(x => x.PriceHistory.HasValue));
        Logger.LogWarning("Symbols with history error:   {Securities}.", securities.Count(x => x.PriceHistory.HasError));
        //foreach (var security in securities.Where(s => s.PriceHistory.HasError).Where(s => !s.PriceHistory.Error.StartsWith("History not found")))
        //    Logger.LogError($"History error for symbol '{security.Symbol}' {security.PriceHistory.Error}");

        Logger.LogWarning("Symbols with base history not set: {Securities}.", securities.Count(x => x.PriceHistoryBase.IsUndefined));
        Logger.LogWarning("Symbols with base history found:   {Securities}.", securities.Count(x => x.PriceHistoryBase.HasValue));
        //foreach (var security in securities.Where(s => s.PriceHistoryBase.HasError).Where(s => !s.PriceHistoryBase.Error.StartsWith("History not found")))
        //    Logger.LogError($"Historybase error for symbol '{security.Symbol}' {security.PriceHistoryBase.Error}");

        LogUnique(securities.Where(s => s.PriceHistory.HasError).Select(s => s.PriceHistory.Error.Message)
        .Concat(securities.Where(s => s.DividendHistory.HasError).Select(s => s.DividendHistory.Error.Message))
        .Concat(securities.Where(s => s.SplitHistory.HasError).Select(s => s.SplitHistory.Error.Message))
        .Concat(securities.Where(s => s.PriceHistoryBase.HasError).Select(s => s.PriceHistoryBase.Error.Message)));
    }

    private void LogUnique(IEnumerable<string> errors)
    {
        List<string> list = errors
            .OrderBy(x => x)
            .Distinct()
            .ToList();

        foreach (var error in list)
            Logger.LogWarning("Unique error: {Error}", error);
    }
}

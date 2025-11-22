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

    public async Task Run(int number, string baseCurrency)
    {
        List<Symbol> symbols = GetSymbols(number);

        Console.WriteLine($"Loaded {symbols.Count} symbols.");
        Console.WriteLine($"Retrieving values...");

        Stopwatch watch = new();
        watch.Start();
        Dictionary<string, Result<History>> results = await YahooQuotes.GetHistoryAsync(symbols.Select(x => x.Name), baseCurrency);
        watch.Stop();

        int n = results.Values.Count(x => x.HasValue);
        double s = watch.Elapsed.TotalSeconds;
        double rate = n / s;
        Logger.LogWarning("Rate = {N}/{S:N2} = {Rate:N2} Hz", n, s, rate);

        Analyze(results);
    }

    private List<Symbol> GetSymbols(int number)
    {
        string path = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}symbols.txt";

        List<string> lines = File
            .ReadAllLines(path)
            .Where(line => !line.StartsWith('#'))
            .Take(number)
            .ToList();

        List<string> errors = lines
            .Where(t => !Symbol.TryCreate(t, out _))
            .ToList();

        if (errors.Count != 0)
            Logger.LogWarning("Invalid symbol names: {names}.", string.Join(", ", errors));

        List<Symbol> symbols = [.. lines
            .Select(t => t.ToSymbol(false))
            .Where(s => s.IsValid)
            .OrderBy(x => x)];

        return symbols;
    }

    private void Analyze(Dictionary<string, Result<History>> dict)
    {
        Logger.LogWarning("Symbols total: {count}.", dict.Count);
        Logger.LogWarning("Symbols not found: {count}.", dict.Count(x => !x.Value.HasValue));

        IEnumerable<(string symbol, History history)> kv = dict.Where(kv => kv.Value.HasValue).Select(x => (x.Key, x.Value.Value));

        List<History> histories = kv.Select(kv => kv.history).ToList();

        Logger.LogWarning("Symbols found: {Count}.", histories.Count);

        Logger.LogWarning("Symbols no currency: {Histories}.", histories.Count(x => x.Currency.Name == ""));
        Logger.LogWarning("Symbols no timezone: {Histories}.", histories.Count(x => x.ExchangeTimezoneName == ""));

        Logger.LogWarning("Symbols with history not set: {Histories}.", histories.Count(x => x.Ticks.Length == 0));

        Logger.LogWarning("Symbols with base history not set: {Histories}.", histories.Count(x => x.BaseTicks.Length == 0));
        //Logger.LogWarning("Symbols with base history found:   {Histories}.", histories.Count(x => x.PriceHistoryBase.HasValue));
        //foreach (var history in histories.Where(s => s.PriceHistoryBase.HasError).Where(s => !s.PriceHistoryBase.Error.StartsWith("History not found")))
        //    Logger.LogError($"Historybase error for symbol '{history.Symbol}' {history.PriceHistoryBase.Error}");

        //LogUnique(histories.Where(s => s.PriceHistory.HasError).Select(s => s.PriceHistory.Error.Message)
        //.Concat(histories.Where(s => s.DividendHistory.HasError).Select(s => s.DividendHistory.Error.Message))
        //.Concat(histories.Where(s => s.SplitHistory.HasError).Select(s => s.SplitHistory.Error.Message))
        //.Concat(histories.Where(s => s.PriceHistoryBase.HasError).Select(s => s.PriceHistoryBase.Error.Message)));
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

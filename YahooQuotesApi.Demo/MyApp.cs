using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YahooQuotesApi.Demo
{
    public class MyApp
    {
        private readonly ILogger Logger;
        private readonly YahooQuotes YahooQuotes;

        public MyApp(ILogger logger)
        {
            Logger = logger;

            Instant start = SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromDays(10));

            YahooQuotes = new YahooQuotesBuilder(Logger)
                .HistoryStarting(start)
                .Build();
        }

        private List<Symbol> GetSymbols(int number)
        {
            const string path = @"..\..\..\symbols.txt";

            List<string> errors = File
                .ReadAllLines(path)
                .Where(line => !line.StartsWith("#"))
                .Take(number)
                .Where(t => Symbol.TryCreate(t) is null)
                .ToList();

            if (errors.Any())
                Logger.LogWarning($"Invalid symbol names: {string.Join(", ", errors)}.");

            List<Symbol> symbols = File
                .ReadAllLines(path)
                .Where(line => !line.StartsWith("#"))
                .Take(number)
                .Select(t => Symbol.TryCreate(t))
                .Where(s => s is not null)
                .Select(s => s!)
                .OrderBy(x => x)
                .ToList();

            return symbols;
        }

        private void Analyze(Dictionary<string, Security?> dict)
        {
            Logger.LogWarning($"Symbols total: {dict.Count}.");
            Logger.LogWarning($"Symbols not found: {dict.Where(x => x.Value is null).Count()}.");

            IEnumerable<KeyValuePair<string, Security>> kvp = dict.Where(kv => kv.Value != null).Cast<KeyValuePair<string, Security>>();
            List<Security> securities = kvp.Select(kv => kv.Value).ToList();

            Logger.LogWarning($"Symbols found: {securities.Count}.");

            Logger.LogWarning($"Symbols no currency: {securities.Where(x => x.Currency == "").Count()}.");
            Logger.LogWarning($"Symbols no timezone: {securities.Where(x => x.ExchangeTimezoneName == "").Count()}.");

            Logger.LogWarning($"Symbols with history not set: {securities.Where(x => x.PriceHistory.HasNothing).Count()}.");
            Logger.LogWarning($"Symbols with history found:   {securities.Where(x => x.PriceHistory.HasValue).Count()}.");
            Logger.LogWarning($"Symbols with history error:   {securities.Where(x => x.PriceHistory.HasError).Count()}.");
            //foreach (var security in securities.Where(s => s.PriceHistory.HasError).Where(s => !s.PriceHistory.Error.StartsWith("History not found")))
            //    Logger.LogError($"History error for symbol '{security.Symbol}' {security.PriceHistory.Error}");

            Logger.LogWarning($"Symbols with base history not set: {securities.Where(x => x.PriceHistoryBase.HasNothing).Count()}.");
            Logger.LogWarning($"Symbols with base history found:   {securities.Where(x => x.PriceHistoryBase.HasValue).Count()}.");
            //foreach (var security in securities.Where(s => s.PriceHistoryBase.HasError).Where(s => !s.PriceHistoryBase.Error.StartsWith("History not found")))
            //    Logger.LogError($"Historybase error for symbol '{security.Symbol}' {security.PriceHistoryBase.Error}");

            LogUnique(securities.Where(s => s.PriceHistory.HasError).Select(s => s.PriceHistory.Error)
            .Concat(securities.Where(s => s.DividendHistory.HasError).Select(s => s.DividendHistory.Error))
            .Concat(securities.Where(s => s.SplitHistory.HasError).Select(s => s.SplitHistory.Error))
            .Concat(securities.Where(s => s.PriceHistoryBase.HasError).Select(s => s.PriceHistoryBase.Error)));
        }

        private void LogUnique(IEnumerable<string> errors)
        {
            List<string> list = errors
                .OrderBy(x => x)
                .Distinct()
                .ToList();

            foreach (var error in list)
                Logger.LogError($"Unique error: {error}");
        }

        public async Task Run(int number, HistoryFlags flags, string baseCurrency)
        {
            List<Symbol> symbols = GetSymbols(number);

            Console.WriteLine($"Loaded {symbols.Count} symbols.");
            Console.WriteLine($"Retrieving values...");

            Stopwatch watch = new();
            watch.Start();
            Dictionary<string, Security?> securities = await YahooQuotes.GetAsync(symbols.Select(x => x.Name), flags, baseCurrency);
            watch.Stop();

            int n = securities.Values.Select(x => x).NotNull().Count();
            double s = watch.Elapsed.TotalSeconds;
            double rate = n / s;
            Logger.LogWarning($"Rate = {n}/{s} = {rate}Hz");

            Analyze(securities);
        }
    }
}

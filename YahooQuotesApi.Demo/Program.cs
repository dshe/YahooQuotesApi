using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace YahooQuotesApi.Demo;

public static class Program
{
    public static async Task Main()
    {
        Console.WriteLine("Please wait...");

        ILogger logger = LoggerFactory
            .Create(x => x
                .AddSimpleConsole(x => x.SingleLine = false)
                .SetMinimumLevel(LogLevel.Debug))
            .CreateLogger("Demo");

        //await new MyApp(logger).Run(10000, HistoryFlags.None, "");
        await new MyApp(logger).Run(100, Histories.All, "JPY=X");

        await Task.Delay(3000);

        Console.WriteLine("Completed!");
        Console.WriteLine("Press a key to exit...");
        Console.ReadLine();
    }
}

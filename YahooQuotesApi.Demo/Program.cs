using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
namespace YahooQuotesApi.Demo;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Please wait...");

        ILogger logger = LoggerFactory
            .Create(x => x
                .AddSimpleConsole(x => x.SingleLine = true)
                .SetMinimumLevel(LogLevel.Warning))
            .CreateLogger("Logger");

        //await new MyApp(logger).Run(10000, HistoryFlags.None, "");
        await new MyApp(logger).Run(300, HistoryFlags.All, "JPY=X");
        Console.ReadLine();
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net;
using System.Threading.Tasks;

namespace YahooQuotesApi.Demo
{
    class Program
    {
        static async Task Main()
        {
            Console.WriteLine("Please wait...");

            ILogger logger = LoggerFactory
                .Create(x => x
                    .AddSimpleConsole(x => x.SingleLine = true)
                    .SetMinimumLevel(LogLevel.Debug))
                .CreateLogger("Logger");

            var myApp = new MyApp(logger);

            //await myApp.Run(1, HistoryFlags.None, "");
            //await myApp.Run(2, HistoryFlags.All, "");
            await myApp.Run(100, HistoryFlags.All, "JPY=X");
            //await myApp.Run(1000, HistoryFlags.None, "");

            Console.ReadLine();
        }
    }
}

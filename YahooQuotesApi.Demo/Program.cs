using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace YahooQuotesApi.Demo
{
    class Program
    {
        static async Task Main()
        {
            var MyHost = new HostBuilder()
                .ConfigureLogging((ctx, logging) => {
                    logging.SetMinimumLevel(LogLevel.Warning);
                    logging.AddDebug();
                    logging.AddConsole();
                })
                .Build();

            var logger = MyHost.Services.GetRequiredService<ILogger<MyApp>>();
            var myApp = new MyApp(logger);

            //await myApp.Run(1, HistoryFlags.None, "");
            //await myApp.Run(2, HistoryFlags.All, "");
            await myApp.Run(1000, HistoryFlags.All, "JPY=X");
            //await myApp.Run(1000, HistoryFlags.None, "");

            Console.ReadLine();

            MyHost.Dispose(); // flushes console!
        }
    }
}

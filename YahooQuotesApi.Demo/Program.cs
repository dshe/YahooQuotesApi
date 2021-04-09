using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
                    //logging.AddConsole();
                    /*
                    logging.AddFile("application.log", config =>
                    {
                        config.Append = true;
                        config.FileSizeLimitBytes = 100_000_000;
                    });
                    */
                })
                .Build();

            var logger = MyHost.Services.GetRequiredService<ILogger<MyApp>>();
            var myApp = new MyApp(logger);

            //await myApp.Run(1, HistoryFlags.None, "");
            //await myApp.Run(2, HistoryFlags.All, "");
            await myApp.Run(1000, HistoryFlags.All, "JPY=X");

            MyHost.Dispose(); // flushes console!
        }
    }
}

using NodaTime;
using System.Threading.RateLimiting;
using YahooQuotesApi;
namespace Xunit.Abstractions;

public abstract class XunitTestBase
{
    private static bool IsRunningOnAppVeyor() => Environment.GetEnvironmentVariable("APPVEYOR") == "True";
    private static readonly RateLimiter limiter = new TokenBucketRateLimiter(
        new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(15),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue
        });

    private readonly ITestOutputHelper Output;
    protected readonly ILoggerFactory LogFactory;
    protected readonly ILogger Logger;
    protected readonly YahooQuotes YahooQuotes;
    protected void Write(string format, params object[] args) => Output.WriteLine(string.Format(format, args));

    protected XunitTestBase(ITestOutputHelper output, LogLevel logLevel = LogLevel.Trace, string name = "Test")
    {
        Output = output;

        LogFactory = LoggerFactory.Create(builder => builder
            .AddMXLogger(output.WriteLine)
            .SetMinimumLevel(logLevel));

        Logger = LogFactory.CreateLogger(name);

        YahooQuotes = new YahooQuotesBuilder()
            .WithLogger(Logger)
            .WithHistoryStartDate(Instant.FromUtc(2024, 10, 1, 0, 0))
            .DoNotUseAdjustedClose()
            .WithHttpRateLimiter(limiter)
            .Build();
    }
}

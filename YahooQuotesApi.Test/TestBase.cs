using Microsoft.Extensions.Logging;
using System;
using Xunit.Abstractions;
namespace YahooQuotesApi.Tests;

public abstract class TestBase
{
    protected readonly Action<string> Write;
    protected readonly ILogger Logger;

    protected TestBase(ITestOutputHelper output, LogLevel logLevel = LogLevel.Debug)
    {
        Write = output.WriteLine;
        Logger = LoggerFactory.Create(cfg => cfg.SetMinimumLevel(logLevel))
            .AddMXLogger(Write)
            .CreateLogger("Test");
    }
}

using Microsoft.Extensions.Logging;
using System;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests;

public abstract class TestBase
{
    protected readonly ILogger Logger;
    protected readonly Action<string> Write;

    protected TestBase(ITestOutputHelper output, LogLevel logLevel = LogLevel.Debug)
    {
        Logger = LoggerFactory
            .Create(builder => builder
                .AddMXLogger(output.WriteLine)
                .SetMinimumLevel(logLevel))
            .CreateLogger("Test");

        Write = (s) => output.WriteLine(s + "\r\n");
    }
}

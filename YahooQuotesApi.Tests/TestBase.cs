using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using MXLogger;

namespace YahooQuotesApi.Tests
{
    public abstract class TestBase
    {
        protected readonly Action<string> Write;
        protected readonly ILogger Logger;

        protected TestBase(ITestOutputHelper output)
        {
            Write = output.WriteLine;
            Logger = new LoggerFactory()
                .AddMXLogger(Write)
                .CreateLogger("Test");
        }
    }
}

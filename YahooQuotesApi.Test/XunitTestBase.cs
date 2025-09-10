﻿using System.Threading.RateLimiting;

namespace Xunit.Abstractions;

public abstract class XunitTestBase
{
    private readonly ITestOutputHelper Output;
    protected readonly ILoggerFactory LogFactory;
    protected readonly ILogger Logger;
    protected void Write(string format, params object[] args) => Output.WriteLine(string.Format(format, args));

    protected XunitTestBase(ITestOutputHelper output, LogLevel logLevel = LogLevel.Trace, string name = "Test")
    {
        Output = output;

        LogFactory = LoggerFactory.Create(builder => builder
            .AddMXLogger(output.WriteLine)
            .SetMinimumLevel(logLevel));

        Logger = LogFactory.CreateLogger(name);
    }

    public static bool IsRunningOnAppVeyor() =>
        Environment.GetEnvironmentVariable("APPVEYOR") == "True";
}

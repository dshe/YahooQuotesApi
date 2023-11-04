using Microsoft.Extensions.Logging;

namespace Xunit.Abstractions;

public abstract class XunitTestBase
{
    private readonly ITestOutputHelper Output;
    protected void Write(string format, params object[] args) => Output.WriteLine(string.Format(format, args));
    protected readonly ILogger Logger;

    protected XunitTestBase(ITestOutputHelper output, LogLevel logLevel = LogLevel.Debug, string name = "Test")
    {
        Output = output;
        Logger = CreateLogger(logLevel, name);
    }

    protected ILogger CreateLogger(LogLevel logLevel, string name)
    {
        return LoggerFactory
            .Create(builder => builder
                .AddMXLogger(Output.WriteLine)
                .SetMinimumLevel(logLevel))
            .CreateLogger(name);
    }
}

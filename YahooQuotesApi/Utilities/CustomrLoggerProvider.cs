namespace YahooQuotesApi;

internal sealed class CustomLoggerProvider : ILoggerProvider
{
    private readonly ILogger Logger;
    internal CustomLoggerProvider(ILogger logger) => Logger = logger;
    public ILogger CreateLogger(string ignoredName) => Logger;
    public void Dispose() { }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace YahooQuotesApi;

public sealed class YahooQuotesBuilder
{
    internal IClock Clock { get; private set; } = SystemClock.Instance;

    private const string ApiDefaultVersion = "v7";
    private string ApiVersion = "";
    internal string BaseUrl { get; private set; }
    private string BaseUrlPattern = "https://query2.finance.yahoo.com/{0}/finance/quote?symbols=";

    public YahooQuotesBuilder(string apiVersion = ApiDefaultVersion)
    {
        ApiVersion = apiVersion;
        BaseUrl = string.Format(BaseUrlPattern, apiVersion);
    }

    internal YahooQuotesBuilder WithClock(IClock clock) // for testing
    {
        Clock = clock;
        return this;
    }

    internal ILogger Logger { get; private set; } = NullLogger.Instance;
    public YahooQuotesBuilder WithLogger(ILogger logger)
    {
        Logger = logger;
        return this;
    }

    internal Instant HistoryStartDate { get; private set; } = Instant.MinValue;
    public YahooQuotesBuilder WithHistoryStartDate(Instant start)
    {
        HistoryStartDate = start;
        return this;
    }

    internal Frequency PriceHistoryFrequency { get; private set; } = Frequency.Daily;
    public YahooQuotesBuilder WithPriceHistoryFrequency(Frequency frequency)
    {
        PriceHistoryFrequency = frequency;
        return this;
    }

    internal Duration SnapshotCacheDuration { get; private set; } = Duration.Zero;
    internal Duration HistoryCacheDuration { get; private set; } = Duration.Zero;
    public YahooQuotesBuilder WithCacheDuration(Duration snapshotCacheDuration, Duration historyCacheDuration)
    {
        if (snapshotCacheDuration > historyCacheDuration)
            throw new ArgumentException("snapshotCacheDuration > historyCacheDuration.");
        SnapshotCacheDuration = snapshotCacheDuration;
        HistoryCacheDuration = historyCacheDuration;
        return this;
    }

    internal bool NonAdjustedClose { get; private set; }
    internal YahooQuotesBuilder WithNonAdjustedClose() // for testing
    {
        NonAdjustedClose = true;
        return this;
    }

    public YahooQuotes Build() => new Services(this)
        .GetServiceProvider()
        .GetRequiredService<YahooQuotes>();
}

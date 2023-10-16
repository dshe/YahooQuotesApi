﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace YahooQuotesApi;

public sealed class YahooQuotesBuilder
{
    public YahooQuotesBuilder() { }

    internal IClock Clock { get; private set; } = SystemClock.Instance;
    internal YahooQuotesBuilder WithClock(IClock clock) // for testing
    {
        Clock = clock;
        return this;
    }

    internal string SnapshotApiVersion { get; private set; } = "v7";
    public YahooQuotesBuilder WithSnapShotApiVersion(string snapshotApiVersion)
    {
        if (string.IsNullOrWhiteSpace(snapshotApiVersion))
            throw new ArgumentNullException(nameof(snapshotApiVersion));
        SnapshotApiVersion = snapshotApiVersion;
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

    internal string SpecificUserAgent { get; private set; } = string.Empty;
    public YahooQuotesBuilder WithSpecificUserAgent(string specificUserAgent)
    {
        SpecificUserAgent = specificUserAgent;
        return this;
    }

    public YahooQuotes Build() => new Services(this)
        .GetServiceProvider()
        .GetRequiredService<YahooQuotes>();
}

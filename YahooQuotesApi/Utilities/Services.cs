using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using System.Net;
using System.Net.Http;
namespace YahooQuotesApi;

// Microsoft.Extensions.Http.Polly
// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0
// Tight coupling between IHttpClientFactory and Microsoft.Extensions.DependencyInjection.
// The pooled HttpMessageHandler instances allow CookieContainer objects to be shared. 
// HttpClient can only be injected inside Typed clients. Otherwise, use IHttpClientFactory.
// https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience?tabs=dotnet-cli
// HttpClient instances created by IHttpClientFactory are intended to be short-lived.
// There is no need to dispose of the HttpClient instances from HttpClientFactory.

internal static class Services
{
    //ILogger Logger;

    internal static YahooQuotes Build(YahooQuotesBuilder yahooQuotesBuilder)
    {
        //yahooQuotesBuilder.Logger.

        return new ServiceCollection()

            .AddNamedHttpClient("", yahooQuotesBuilder)
            .AddNamedHttpClient("HttpV2", yahooQuotesBuilder)

            .AddSingleton(yahooQuotesBuilder)
            .AddSingleton(yahooQuotesBuilder.Clock)
            .AddSingleton(yahooQuotesBuilder.Logger)
            .AddSingleton<CookieAndCrumb>()
            .AddSingleton<YahooSnapshot>()
            .AddSingleton<SnapshotCreator>()
            .AddSingleton<YahooHistory>()
            .AddSingleton<HistoryCreator>()
            .AddSingleton<HistoryBasePricesCreator>()
            .AddSingleton<YahooModules>()
            .AddSingleton<YahooQuotes>()

            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true })

            .GetRequiredService<YahooQuotes>();
    }

    private static IServiceCollection AddNamedHttpClient(this IServiceCollection serviceCollection, string name, YahooQuotesBuilder builder)
    {
        return serviceCollection

            .AddHttpClient(name, client =>
            {
                //client.Timeout = TimeSpan.FromSeconds(10); // default: 100 seconds
                if (name == "HttpV2")
                {
                    client.DefaultRequestVersion = new Version(2, 0);
                    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
                }
                if (!string.IsNullOrEmpty(builder.HttpUserAgent))
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(builder.HttpUserAgent);
                else
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentGenerator.GetRandom());
            })

            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = false,
                //MaxConnectionsPerServer: default is int.MaxValue; with HTTP/2, every request tends to reuse the same connection
                //CookieContainer = new CookieContainer(),
                UseCookies = false, // Important since these handlers may be reused.
            })

            .AddStandardResilience(builder.WithHttpResilience)

            .Services;
    }

    private static IHttpClientBuilder AddStandardResilience(this IHttpClientBuilder builder, bool add)
    {
        if (!add)
            return builder;

        /* Automatic resilience policies applied:
         retries for 429, 503, and transient errors
         Timeout handling
         circuit breaking
         Retry - after header support(handled automatically)
        */
        builder.AddStandardResilienceHandler();
        /*
        .AddStandardResilienceHandler(options =>
        {
            //options.Retry.MaxRetryAttempts = 3;
            //options.Retry.BackoffType = DelayBackoffType.Exponential;
            //options.Retry.Delay = TimeSpan.FromSeconds(2); // initial delay
            options.Retry.ShouldRetryAfterHeader = true;
            //options.Retry.UseJitter = true;
            //options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            //options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            //options.CircuitBreaker.FailureRatio = 0.2;
            //options.CircuitBreaker.MinimumThroughput = 20;
            //options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(20);
            //options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            options.RateLimiter = new HttpRateLimiterStrategyOptions
            {
                DefaultRateLimiterOptions = new System.Threading.RateLimiting.ConcurrencyLimiterOptions
                {
                    QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                    PermitLimit = 1, // max concurrent
                    QueueLimit = 0,
                }
            };
        })
        */
        return builder;
    }
}

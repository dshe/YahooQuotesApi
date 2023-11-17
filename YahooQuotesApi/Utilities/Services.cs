using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using YahooQuotesApi.Crumb;
using YahooQuotesApi.Utilities;

namespace YahooQuotesApi;

// Microsoft.Extensions.Http.Polly
// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0
// Tight coupling between IHttpClientFactory and Microsoft.Extensions.DependencyInjection.
// The pooled HttpMessageHandler instances allow CookieContainer objects to be shared. 
// HttpClient can only be injected inside Typed clients. Otherwise, use IHttpClientFactory.

internal sealed class Services
{
    private readonly YahooQuotesBuilder YahooQuotesBuilder;
    internal Services(YahooQuotesBuilder yahooQuotesBuilder) =>
        YahooQuotesBuilder = yahooQuotesBuilder;

    internal ServiceProvider GetServiceProvider()
    {
        string httpUserAgent = YahooQuotesBuilder.HttpUserAgent;

        return new ServiceCollection()

            .AddNamedHttpClient("crumb",    httpUserAgent)
            .AddNamedHttpClient("snapshot", httpUserAgent)
            .AddNamedHttpClient("history",  httpUserAgent)
            .AddNamedHttpClient("modules",  httpUserAgent)

            .AddLogging(configure => configure
                .ClearProviders()
                .AddProvider(new CustomLoggerProvider(YahooQuotesBuilder.Logger)))

            .AddSingleton(YahooQuotesBuilder)
            .AddSingleton<YahooQuotes>()
            .AddSingleton<Quotes>()
            .AddSingleton<YahooCrumb>()
            .AddSingleton<YahooSnapshot>()
            .AddSingleton<YahooHistory>()
            .AddSingleton<HistoryBaseComposer>()
            .AddSingleton<YahooModules>()

            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}

internal static partial class Xtensions
{
    internal static IServiceCollection AddNamedHttpClient(this IServiceCollection serviceCollection, string name, string httpUserAgent)
    {
        return serviceCollection

            .AddHttpClient(name, client =>
            {
                //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //client.Timeout = Timeout.InfiniteTimeSpan; // default: 100 seconds
                client.DefaultRequestVersion = new Version(2, 0);
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

                if (!string.IsNullOrEmpty(httpUserAgent))
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(httpUserAgent);
                else if (name == "crumb")
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentGenerator.GetRandomUserAgent()); // ???

                if (name == "snapshot" || name == "modules")
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })

            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = false,
                //MaxConnectionsPerServer: default is int.MaxValue; with HTTP/2, every request tends to reuse the same connection
                //CookieContainer = new CookieContainer(),
                UseCookies = false
            })

            .AddStandardResilienceHandler(static options => 
            {
                //options.RateLimiter
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.Retry.BackoffType = DelayBackoffType.Linear;
                options.Retry.MaxRetryAttempts = 5;
                //options.CircuitBreaker.ManualControl = new CircuitBreakerManualControl(isIsolated: true);
                //options.AttemptTimeout.
            })
            
            .Services;
    }
}


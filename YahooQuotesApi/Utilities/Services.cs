using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;
using System.Net;
using System.Net.Http;
using YahooQuotesApi.Crumb;

namespace YahooQuotesApi;

//Microsoft.Extensions.Http.Polly
//https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0
//Tight coupling between IHttpClientFactory and Microsoft.Extensions.DependencyInjection.
//The pooled HttpMessageHandler instances allow CookieContainer objects to be shared. 
//HttpClient can only be injected inside Typed clients. Otherwise, use IHttpClientFactory.

internal sealed class Services
{
    private readonly IClock Clock;
    private readonly ILogger Logger;
    private readonly YahooQuotesBuilder YahooQuotesBuilder;
    private readonly AsyncTimeoutPolicy<HttpResponseMessage> TimeoutPolicy;
    private readonly AsyncRetryPolicy<HttpResponseMessage> RetryPolicy;

    internal Services(YahooQuotesBuilder yahooQuotesBuilder)
    {
        YahooQuotesBuilder = yahooQuotesBuilder;
        Clock = yahooQuotesBuilder.Clock;
        Logger = yahooQuotesBuilder.Logger;

        TimeoutPolicy = Policy.
            TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(20)); // Timeout for an individual try

        RetryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>() // thrown by TimeoutPolicy
            .WaitAndRetryAsync(new[]
            {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(10)
            },
            onRetry: (r, ts, n, ctx) =>
            {
                if (r.Result == default) // no result, an exception was thrown
                    Logger.LogError(r.Exception, "Retry[{N}]: {Message}", n, r.Exception.Message);
                else
                    Logger.LogError("Retry[{N}]: ({StatusCode}) {ReasonPhrase}", n, r.Result.StatusCode, r.Result.ReasonPhrase);
            });
    }

    internal ServiceProvider GetServiceProvider()
    {
        return new ServiceCollection()
            .AddNamedHttpClient("crumb")
            .Services

            .AddNamedHttpClient("snapshot")
            .AddPolicyHandler(TimeoutPolicy)
            .AddPolicyHandler(RetryPolicy)
            .Services

            .AddNamedHttpClient("history")
            .AddPolicyHandler(TimeoutPolicy)
            .AddPolicyHandler(RetryPolicy)
            .Services

            .AddNamedHttpClient("modules")
            .AddPolicyHandler(TimeoutPolicy)
            .AddPolicyHandler(RetryPolicy)
            .Services

            .AddSingleton(Clock)
            .AddSingleton(Logger)
            .AddSingleton(YahooQuotesBuilder)
            .AddSingleton<YahooQuotes>()
            .AddSingleton<Quotes>()
            .AddSingleton<YahooSnapshot>()
            .AddSingleton<YahooHistory>()
            .AddSingleton<HistoryBaseComposer>()
            .AddSingleton<YahooModules>()
            .AddSingleton<YahooCrumb>()

            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}

internal static partial class Xtensions
{
    internal static IHttpClientBuilder AddNamedHttpClient(this IServiceCollection serviceCollection, string name)
    {
        return serviceCollection

            .AddHttpClient(name, client =>
            {
                //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //client.Timeout = Timeout.InfiniteTimeSpan; // default: 100 seconds
                client.DefaultRequestVersion = new Version(2, 0);
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            })

            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = false,
                //MaxConnectionsPerServer: default is int.MaxValue; with HTTP/2, every request tends to reuse the same connection
                //CookieContainer = new CookieContainer(),
                UseCookies = false
            });
    }
}

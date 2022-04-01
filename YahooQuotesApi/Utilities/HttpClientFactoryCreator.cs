using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using Polly.Retry;
using Polly.CircuitBreaker;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
namespace YahooQuotesApi;

//https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0
//Tight coupling between IHttpClientFactory and Microsoft.Extensions.DependencyInjection
//The pooled HttpMessageHandler instances results in CookieContainer objects being shared. 

internal class HttpClientFactoryCreator
{
    private readonly ILogger Logger;
    internal HttpClientFactoryCreator(ILogger logger) => Logger = logger;

    internal IHttpClientFactory Create()
    {
        return new ServiceCollection()

            .AddHttpClient("snapshot", client =>
            {
                //client.Timeout = Timeout.InfiniteTimeSpan; // default: 100 seconds
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestVersion = new Version(2, 0);
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = false,
                //MaxConnectionsPerServer = 1 // The default: int.MaxValue
            })
            //.SetHandlerLifetime(Timeout.InfiniteTimeSpan) // default: 2 minutes
            .AddPolicyHandler(RetryPolicy)
            .AddPolicyHandler(TimeoutPolicy)
            .AddPolicyHandler(CircuitBreakerPolicy)
            .Services

            .AddHttpClient("history", client =>
            {
                //client.Timeout = Timeout.InfiniteTimeSpan; // default: 100 seconds
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");
                client.DefaultRequestVersion = new Version(2, 0);
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = false,
                CookieContainer = new CookieContainer(),
                //MaxConnectionsPerServer = 1 // The default: int.MaxValue
            })
            //.SetHandlerLifetime(Timeout.InfiniteTimeSpan) // default is 2 minutes
            .AddPolicyHandler(RetryPolicy)
            .AddPolicyHandler(TimeoutPolicy)
            .AddPolicyHandler(CircuitBreakerPolicy)
            .Services

            .BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();
    }

    private readonly AsyncTimeoutPolicy<HttpResponseMessage> TimeoutPolicy =
        Policy.TimeoutAsync<HttpResponseMessage>(20); // Timeout for an individual try

    private AsyncRetryPolicy<HttpResponseMessage> RetryPolicy =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>() // thrown by Polly's TimeoutPolicy if the inner call times out
            .WaitAndRetryAsync(new[]
            {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
            },
            onRetry: (r, ts, n, ctx) =>
            {
                if (r.Result == default) // no result, an exception was thrown
                    Logger.LogError(r.Exception, "Retry[{N}]: {Message}", n, r.Exception.Message);
                else
                    Logger.LogError("Retry[{N}]: ({StatusCode}) {ReasonPhrase}", n, r.Result.StatusCode, r.Result.ReasonPhrase);
            });

    private AsyncCircuitBreakerPolicy<HttpResponseMessage> CircuitBreakerPolicy =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (r, ts, ctx) =>
                {
                    if (r.Result == default) // no result, an exception was thrown
                        Logger.LogError(r.Exception, "Circuit Breaking: {Message}", r.Exception.Message);
                    else
                        Logger.LogError("Circuit Breaking: ({StatusCode}) {ReasonPhrase}", r.Result.StatusCode, r.Result.ReasonPhrase);
                },
                onReset: ctx =>
                {
                    Logger.LogWarning($"Circuit Resetting...");
                });
}

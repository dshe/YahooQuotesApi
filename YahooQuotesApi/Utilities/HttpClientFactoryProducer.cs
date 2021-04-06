using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Polly;
using Polly.Timeout;
using Polly.Extensions;
using Polly.Extensions.Http;

//https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0
// The pooled HttpMessageHandler instances results in CookieContainer objects being shared. 

namespace YahooQuotesApi
{
    internal class HttpClientFactoryProducer
    {
        private readonly ServiceProvider ServiceProvider;

        internal IHttpClientFactory Produce() =>
            ServiceProvider.GetRequiredService<IHttpClientFactory>();

        internal HttpClientFactoryProducer(ILogger logger)
        {
            var retryPolicy = HttpPolicyExtensions
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
                        logger.LogError(r.Exception, $"Retry[{n}]: {r.Exception.Message}");
                    else
                        logger.LogError($"Retry[{n}]: ({r.Result.StatusCode}) {r.Result.ReasonPhrase}");
                });

            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10); // Timeout for an individual try

            var circuitBreakerPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (r, ts, ctx) =>
                    {
                        if (r.Result == default) // no result, an exception was thrown
                            logger.LogError(r.Exception, $"Circuit Breaking: {r.Exception.Message}");
                        else
                            logger.LogError($"Circuit Breaking: ({r.Result.StatusCode}) {r.Result.ReasonPhrase}");
                    },
                    onReset: ctx =>
                    {
                        logger.LogError($"Circuit Resetting...");
                    });


            ServiceProvider = new ServiceCollection()

            .AddHttpClient("snapshot", client =>
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
            .AddPolicyHandler(retryPolicy)
            //.AddPolicyHandler(timeoutPolicy)
            .AddPolicyHandler(circuitBreakerPolicy)
            .Services

            .AddHttpClient("history", client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = new CookieContainer(),
                MaxConnectionsPerServer = 64 // The default is int.MaxValue
            })
            .AddPolicyHandler(retryPolicy)
            //.AddPolicyHandler(timeoutPolicy)
            .AddPolicyHandler(circuitBreakerPolicy)
            .Services

            .BuildServiceProvider();
        }
    }
}

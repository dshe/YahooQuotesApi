using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

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
    internal static YahooQuotes Build(YahooQuotesBuilder yahooQuotesBuilder)
    {
        return new ServiceCollection()

            .AddNamedHttpClient("", yahooQuotesBuilder)
            .AddNamedHttpClient("HttpV2", yahooQuotesBuilder)

            .AddSingleton(yahooQuotesBuilder)
            .AddSingleton(yahooQuotesBuilder.Clock)
            .AddSingleton(yahooQuotesBuilder.Logger)
            .AddSingleton<YahooQuotes>()
            .AddSingleton<Quotes>()
            .AddSingleton<CookieAndCrumb>()
            .AddSingleton<YahooSnapshot>()
            .AddSingleton<YahooHistory>()
            .AddSingleton<HistoryBaseComposer>()
            .AddSingleton<YahooModules>()

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
        if (add)
            builder.AddStandardResilienceHandler();
        return builder;
    }    
}


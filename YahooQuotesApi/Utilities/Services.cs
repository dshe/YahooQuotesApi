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

            .AddNamedHttpClient("crumb",    yahooQuotesBuilder)
            .AddNamedHttpClient("snapshot", yahooQuotesBuilder)
            .AddNamedHttpClient("history",  yahooQuotesBuilder)
            .AddNamedHttpClient("modules",  yahooQuotesBuilder)

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
        string httpUserAgent = builder.HttpUserAgent;

        return serviceCollection

            .AddHttpClient(name, client =>
            {
                //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //client.Timeout = Timeout.InfiniteTimeSpan; // default: 100 seconds
                //client.Timeout = TimeSpan.FromSeconds(10); // default: 100 seconds
                client.DefaultRequestVersion = new Version(2, 0);
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
 
                if (!string.IsNullOrEmpty(httpUserAgent))
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(httpUserAgent);
                else if (name == "crumb")
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentGenerator.GetRandomUserAgent());

                if (name == "snapshot" || name == "modules")
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })

            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = false,
                //MaxConnectionsPerServer: default is int.MaxValue; with HTTP/2, every request tends to reuse the same connection
                //CookieContainer = new CookieContainer(),
                UseCookies = false // Important since these handlers may be reused.
            })

            //.AddStandardResilienceHandler(builder.HttpResilienceOptions)
            .AddStandardResilienceHandler()

            .Services;
    }
}


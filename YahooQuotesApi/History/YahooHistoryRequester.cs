using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace YahooQuotesApi;

internal class YahooHistoryRequester
{
    private readonly ILogger Logger;
    private readonly IHttpClientFactory HttpClientFactory;
    private readonly SemaphoreSlim Semaphore = new(1, 1);
    private string Crumb = "";
    private HttpClient? HttpClient;
    private bool reset = true;

    internal YahooHistoryRequester(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        Logger = logger;
        HttpClientFactory = httpClientFactory;
    }

    internal async Task<HttpResponseMessage> Request(string url, CancellationToken ct)
    {
        bool retry = false;
        while (true)
        {
            await Semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (reset)
                {
                    (HttpClient, Crumb) = await Reset(ct).ConfigureAwait(false);
                    reset = false;
                }
            }
            finally
            {
                Semaphore.Release();
            }

            UriBuilder ub = new(url);
            ub.Query += $"&crumb={Crumb}";

            using HttpRequestMessage request = new(HttpMethod.Get, ub.Uri);
            HttpResponseMessage response = await HttpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized && !retry)
            {
                if (!reset)
                {
                    reset = true;
                    retry = true;
                    Logger.LogWarning("HttpStatusCode: Unauthorized. Retrying...");
                }
                continue;
            }
            return response;
        }
    }

    private async Task<(HttpClient httpClient, string crumb)> Reset(CancellationToken ct)
    {
        Logger.LogInformation($"YahooHistory: obtaining crumb.");
        HttpClient httpClient = await CreateHttpClient(ct).ConfigureAwait(false);
        Uri uri = new("https://query1.finance.yahoo.com/v1/test/getcrumb");
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string crumb = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return (httpClient, crumb);
    }

    private async Task<HttpClient> CreateHttpClient(CancellationToken ct)
    {
        const int MaxRetryCount = 3;
        for (int retryCount = 0; retryCount < MaxRetryCount; retryCount++)
        {
            // HttpClientFactoryProducer is configured so that a new CookieContainer() is created for each new HttpClient created
            HttpClient httpClient = HttpClientFactory.CreateClient("history");

            // random query to avoid cached response and set new cookie
            Uri uri = new($"https://finance.yahoo.com?{Xtensions.GetRandomString(8)}");
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode
                && response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies)
                && setCookies.Any())
                return httpClient;

            Logger.LogWarning("YahooHistory: failure({StatusCode}) to create client. Retrying...", response.StatusCode);

            await Task.Delay(500, ct).ConfigureAwait(false);
        }
        throw new InvalidOperationException("YahooHistory: failure to reset HttpClient.");
    }
}

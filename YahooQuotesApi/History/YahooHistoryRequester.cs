using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    internal class YahooHistoryRequester
    {
        private readonly ILogger Logger;
        private readonly IHttpClientFactory HttpClientFactory;
        private readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        private string Crumb = "";
        private HttpClient? HttpClient;
        private bool reset = true;

        internal YahooHistoryRequester(ILogger logger, IHttpClientFactory httpClientFactory)
        {
            Logger = logger;
            HttpClientFactory = httpClientFactory;
        }

        internal async Task<HttpResponseMessage> Request(Uri uri, CancellationToken ct)
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

                var ub = new UriBuilder(uri);
                ub.Query += $"&crumb={Crumb}";

                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, ub.Uri) { Version = new Version(2, 0) };
                HttpResponseMessage response = await HttpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized && !retry)
                {
                    if (!reset)
                    {
                        reset = true;
                        retry = true;
                        Logger.LogError("HttpStatusCode: Unauthorized. Retrying...");
                    }
                    continue;
                }
                return response;
            }
        }

        private async Task<(HttpClient,string)> Reset(CancellationToken ct)
        {
            Logger.LogInformation($"YahooHistory: obtaining crumb.");
            var httpClient = await CreateHttpClient(ct).ConfigureAwait(false);
            var uri = new Uri("https://query1.finance.yahoo.com/v1/test/getcrumb");
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri) { Version = new Version(2, 0) };
            using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var crumb = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return (httpClient, crumb);
        }

        private async Task<HttpClient> CreateHttpClient(CancellationToken ct)
        {
            const int MaxRetryCount = 3;
            for (int retryCount = 0; retryCount < MaxRetryCount; retryCount++)
            {
                // HttpClientFactoryProducer is configured so that a new CookieContainer() is created for each new HttpClient created
                var httpClient = HttpClientFactory.CreateClient("history");

                // random query to avoid cached response and set new cookie
                var uri = new Uri($"https://finance.yahoo.com?{Extensions.GetRandomString(8)}");
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri) { Version = new Version(2, 0)};
                using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode 
                    && response.Headers.TryGetValues("Set-Cookie", out var setCookies)
                    && setCookies.Any())
                        return httpClient;

                Logger.LogError($"YahooHistory: failure({response.StatusCode}) to create client. Retrying...");

                await Task.Delay(500, ct).ConfigureAwait(false);
            }
            throw new InvalidOperationException("YahooHistory: failure to reset HttpClient.");
        }
    }
}

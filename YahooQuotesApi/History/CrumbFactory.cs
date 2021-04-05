using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace YahooQuotesApi
{
    internal class CrumbFactory
    {
        private readonly ILogger Logger;
        private readonly IHttpClientFactory HttpClientFactory;
        private readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        private string? Crumb;

        internal CrumbFactory(ILogger logger, IHttpClientFactory factory)
        {
            Logger = logger;
            HttpClientFactory = factory;
        }

        internal async Task<string> GetCrumbAsync(bool reset, CancellationToken ct)
        {
            await Semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (reset || Crumb == null)
                {
                    await ResetAsync(ct).ConfigureAwait(false);
                    var response = await HttpClientFactory.CreateClient("history").GetAsync("https://query1.finance.yahoo.com/v1/test/getcrumb", ct)
                        .ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    Crumb = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                return Crumb;
            }
            finally
            {
                Semaphore.Release();
            }
        }

        private async Task ResetAsync(CancellationToken ct)
        {
            const int MaxRetryCount = 5;
            for (int retryCount = 0; retryCount < MaxRetryCount; retryCount++)
            {
                // random query to avoid cached response
                var uri = new Uri($"https://finance.yahoo.com?{Utility.GetRandomString(8)}");

                var client = HttpClientFactory.CreateClient("history");
                var response = await client.GetAsync(uri, ct).ConfigureAwait(false);

                //if (response.IsSuccessStatusCode && handler.CookieContainer.Count > 0)
                if (response.IsSuccessStatusCode)
                        return;

                Logger.LogError($"YahooHistory: failure({response.StatusCode}) to create client. Retrying...");

                await Task.Delay(500, ct).ConfigureAwait(false);
            }
            throw new InvalidOperationException("YahooHistory: failure to create HttpClient.");
        }
    }
}

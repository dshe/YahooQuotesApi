using System.Net.Http;

namespace YahooQuotesApi.Crumb;

public sealed class YahooCrumb : IDisposable
{
    private readonly ILogger Logger;
    private readonly HttpClient HttpClient;
    private readonly SemaphoreSlim SemaphoreSlim = new(1, 1);
    private (List<string>, string) CookieAndCrumb;

    public YahooCrumb(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        Logger = logger;
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        HttpClient = httpClientFactory.CreateClient("crumb");
    }

    public async Task<(List<string>, string)> GetCookieAndCrumb(CancellationToken ct)
    {
        await SemaphoreSlim.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (CookieAndCrumb == default)
            {
                var cookies = await GetCookies(ct).ConfigureAwait(false);
                var crumb = await GetCrumb(cookies, ct).ConfigureAwait(false);
                CookieAndCrumb = (cookies, crumb);
            }
            return CookieAndCrumb;
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "GetCookieAndCrumb: error");
            throw;
        }
        finally
        {
            SemaphoreSlim.Release(); 
        }
    }

    private async Task<List<string>> GetCookies(CancellationToken ct)
    {
        //Uri url = new("https://finance.yahoo.com/");
        // Uri url = new("https://fc.yahoo.com/"); // timed out
        Uri url = new("https://login.yahoo.com/");
        //Uri url = new("https://api.login.yahoo.com/");

        // This call may result in a 404 error,
        // but we just need it to extract set-cookie from the response headers
        // which is then used in subsequent calls
        using HttpResponseMessage response = await HttpClient.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookie))
            throw new InvalidOperationException("Set-Cookie header was not present in the response from " + url + ".");
        List<string> cookies = setCookie.ToList();
        Logger.LogTrace(message: "GetCookieAndCrumb: received these cookies({Count}): {Cookies}", cookies.Count, Environment.NewLine + string.Join(Environment.NewLine, cookies));
        //List<string> cookies = setCookie.Where(c => c.StartsWith("A3=d", StringComparison.OrdinalIgnoreCase)).ToList();
        cookies = cookies
            .Where(c => c.Contains("Domain=.yahoo.com", StringComparison.OrdinalIgnoreCase))
            .Where(c => c.Contains("SameSite=None", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (!cookies.Any())
            throw new InvalidOperationException("No useful cookies found.");
        Logger.LogTrace("GetCookieAndCrumb: using these cookies({Count}): {Cookies}", cookies.Count, Environment.NewLine + string.Join(Environment.NewLine, cookies));
        return cookies;
    }

    private async Task<string> GetCrumb(List<string> cookies, CancellationToken ct = default)
    {
        // Now make an HTTP GET call, by including the obtained cookie from the previous response headers.
        // This call will retrieve the crumb value.
        HttpClient.DefaultRequestHeaders.Add("cookie", cookies);
        Uri url = new("https://query2.finance.yahoo.com/v1/test/getcrumb");
        using HttpResponseMessage response = await HttpClient.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string crumb = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(crumb))
            throw new HttpRequestException($"Could not generate crumb from {url} using cookies.");
        return crumb;

        // Cache the cookie and crumb values to use with further requests.
        // httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
        // Example: query2.finance.yahoo.com/v7/finance/quote?symbols={symbol}&crumb={crumb}
    }

    public void Dispose()
    {
        HttpClient.Dispose();
        SemaphoreSlim.Dispose();
    }
}

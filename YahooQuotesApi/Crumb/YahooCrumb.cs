using System.Net.Http;

namespace YahooQuotesApi.Crumb;

public sealed class YahooCrumb
{
    private readonly ILogger Logger;
    private readonly HttpClient HttpClient;

    public YahooCrumb(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        Logger = logger;
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        HttpClient = httpClientFactory.CreateClient("crumb");
    }

    public async Task<(List<string>, string)> GetCookieAndCrumb(CancellationToken ct) =>
        await GetLazy(ct).Value.ConfigureAwait(false);
    private Lazy<Task<(List<string>, string)>> GetLazy(CancellationToken ct) =>
        new(async () => await Get(ct).ConfigureAwait(false));
    private async Task<(List<string>, string)> Get(CancellationToken ct)
    {
        Logger.LogDebug("GetCookieAndCrumb: start");
        try
        {
            List<string> cookies = await GetCookies(ct).ConfigureAwait(false);
            string crumb = await GetCrumb(cookies, ct).ConfigureAwait(false);
            return (cookies, crumb);
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "GetCookieAndCrumb: error");
            throw;
        }
    }

    private async Task<List<string>> GetCookies(CancellationToken ct)
    {
        // Uri url = new("https://fc.yahoo.com/"); // timed out
        Uri url = new("https://login.yahoo.com");

        // This call may result in a 404 error,
        // but we just need it to extract set-cookie from the response headers
        // which is then used in subsequent calls
        using HttpResponseMessage response = await HttpClient.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookie))
            throw new InvalidOperationException("Set-Cookie header was not present in the response from " + url + ".");
        Logger.LogDebug("GetCookieAndCrumb: received these cookies: {Cookies}", string.Join(',', setCookie));
        //List<string> cookies = setCookie.Where(c => c.StartsWith("A3=d", StringComparison.OrdinalIgnoreCase)).ToList();
        List<string> cookies = setCookie
            .Where(c => c.Contains("domain=.yahoo.com", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (!cookies.Any())
            throw new InvalidOperationException("No useful cookies found.");
        Logger.LogDebug("GetCookieAndCrumb: using these cookies: {Cookies}", string.Join(',', cookies));
        return cookies;
    }

    private async Task<string> GetCrumb(List<string> cookies, CancellationToken ct = default)
    {
        // Now make an HTTP GET call, by including the obtained cookie from the previous response headers.
        // This call will retrieve the crumb value.
        HttpClient.DefaultRequestHeaders.Add("cookie", cookies);
        Uri url = new("https://query2.finance.yahoo.com/v1/test/getcrumb");
        using HttpResponseMessage response = await HttpClient.GetAsync(url, ct).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e) 
        {
            Logger.LogError(e, "GetCookieAndCrumb: response error from {Url}.", "https://query2.finance.yahoo.com/v1/test/getcrumb");
            throw;
        }

        string crumb = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(crumb))
            throw new HttpRequestException($"Could not generate crumb from {url} using cookies.");

        // Cache the cookie and crumb values to use with further requests.
        // httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
        // Example: query2.finance.yahoo.com/v7/finance/quote?symbols={symbol}&crumb={crumb}
        return crumb;
    }
}

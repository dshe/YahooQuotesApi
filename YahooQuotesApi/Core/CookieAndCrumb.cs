using System.Net.Http;

namespace YahooQuotesApi;

public sealed class CookieAndCrumb
{
    private readonly object LockObj = new();
    private readonly ILogger Logger;
    private readonly HttpClient HttpClient;
    private Task<(List<string>, string)>? TheTask;

    public CookieAndCrumb(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        Logger = logger;
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        HttpClient = httpClientFactory.CreateClient("crumb");
    }

    public async Task<(List<string>, string)> Get(CancellationToken ct)
    {
        lock (LockObj)
        {
            if (TheTask == null)
                TheTask = GetCookieAndCrumb1(ct); // start task
        }
        return await TheTask.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<(List<string>, string)> GetCookieAndCrumb1(CancellationToken ct)
    {
        try
        {
            var cookies = await GetCookies(ct).ConfigureAwait(false);
            var crumb = await GetCrumb(cookies, ct).ConfigureAwait(false);
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
        //Uri uri = new("https://finance.yahoo.com/");
        // Uri uri = new("https://fc.yahoo.com/"); // timed out
        Uri uri = new("https://login.yahoo.com/");
        //Uri uri = new("https://api.login.yahoo.com/");

        // This call may result in a 404 error,
        // but we just need it to extract set-cookie from the response headers
        // which is then used in subsequent calls
        using HttpResponseMessage response = await HttpClient.GetAsync(uri, ct).ConfigureAwait(false);
        if (!response.Headers.TryGetValues(name: "Set-Cookie", out var setCookie))
            throw new InvalidOperationException("Set-Cookie header was not present in the response from " + uri + ".");

        List<string> cookies = setCookie.ToList();
        Logger.LogTrace(message: "GetCookieAndCrumb: received these cookies({Count}): {Cookies}", cookies.Count, Environment.NewLine + string.Join(Environment.NewLine, cookies));
        cookies = cookies
            .Where(c => c.Contains("Domain=.yahoo.com", StringComparison.OrdinalIgnoreCase))
            .Where(c => c.Contains("SameSite=None", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (!cookies.Any())
            throw new InvalidOperationException("No useful cookies found.");
        Logger.LogTrace(message: "GetCookieAndCrumb: using these cookies({Count}): {Cookies}", cookies.Count, Environment.NewLine + string.Join(Environment.NewLine, cookies));
        // The cookies expire in 1 year.
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
            throw new HttpRequestException($"Could not receive crumb from {url} using cookies.");
        return crumb;
    }
}

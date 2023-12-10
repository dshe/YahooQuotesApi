using System.Net.Http;

namespace YahooQuotesApi;

public sealed class CookieAndCrumb
{
    private readonly object LockObj = new();
    private readonly ILogger Logger;
    private readonly HttpClient HttpClient;
    private readonly Uri CookieUri;
    private readonly Uri CrumbUri;
    private Task<(List<string>, string)>? TheTask;

    public CookieAndCrumb(YahooQuotesBuilder builder, IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));
        Logger = builder.Logger;
        CookieUri = builder.CookieUri;
        CrumbUri = builder.CrumbUri;
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
        // This call may result in a 404 error,
        // but we just need it to extract set-cookie from the response headers
        // which is then used in subsequent calls
        using HttpResponseMessage response = await HttpClient.GetAsync(CookieUri, ct).ConfigureAwait(false);
        if (!response.Headers.TryGetValues(name: "Set-Cookie", out IEnumerable<string>? setCookie))
            throw new InvalidOperationException($"Set-Cookie header was not present in the response from {CookieUri}.");
        List<string> cookies = setCookie.ToList();
        Logger.LogTrace("GetCookies: received these cookies({Count}): {Cookies}", cookies.Count, FormatCookies(cookies));
        if (!cookies.Any())
            throw new InvalidOperationException($"No cookies returned in the response from {CookieUri}.");
        cookies = cookies
            .Where(c => c.Contains("Domain=.yahoo.com", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (!cookies.Any())
            throw new InvalidOperationException($"No useful cookies returned in the response from {CookieUri}.");
        Logger.LogTrace("GetCookies: using these cookies({Count}): {Cookies}", cookies.Count, FormatCookies(cookies));
        // The cookies expire in 1 year.
        return cookies;
    }

    private async Task<string> GetCrumb(List<string> cookies, CancellationToken ct = default)
    {
        // Make an HTTP GET call, including the obtained cookie from the previous response.
        HttpClient.DefaultRequestHeaders.Add("cookie", cookies);
        using HttpResponseMessage response = await HttpClient.GetAsync(CrumbUri, ct).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Did not receive crumb from {CrumbUri} using cookies.", ex);
        }
        string crumb = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(crumb))
            throw new InvalidOperationException($"Did not receive crumb from {CookieUri} using cookies.");

        Logger.LogTrace("GetCrumb: received crumb {Crumb}", crumb);
        return crumb;
    }

    private static string FormatCookies(IEnumerable<string> cookies) =>
        Environment.NewLine + string.Join(Environment.NewLine, cookies);
}

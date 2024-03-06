using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace YahooQuotesApi;

public sealed class CookieAndCrumb
{
    private readonly object LockObj = new();
    private readonly ILogger Logger;
    private readonly IHttpClientFactory HttpClientFactory;
    private Task<(List<string>, string)>? TheTask;

    public CookieAndCrumb(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        Logger = logger;
        HttpClientFactory = httpClientFactory;
    }

    public async Task<(List<string>, string)> Get(CancellationToken ct)
    {
        // Lazy<Task<T>> does not support cancellation.
        lock (LockObj)
        {
            TheTask ??= GetCookieAndCrumb1(ct); // start the task if not already started
        }

        return await TheTask.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<(List<string>, string)> GetCookieAndCrumb1(CancellationToken ct)
    {
        try
        {
            List<string> cookies = await GetCookies(ct).ConfigureAwait(false);
            if (!cookies.Any())
                cookies = await GetEuropeanCookies(ct).ConfigureAwait(false);
            if (!cookies.Any())
            {
                Logger.LogCritical("No cookies found.");
                throw new InvalidOperationException("No cookies found.");
            }

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
        // "https://fc.yahoo.com/"  => time out
        Uri uri = new("https://login.yahoo.com/");
        HttpClient httpClient = HttpClientFactory.CreateClient("cookie");
        // This call may result in a 404 error, which may be ignored.
        using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.Headers.TryGetValues(name: "Set-Cookie", out IEnumerable<string>? setCookie))
        {
            Logger.LogTrace("Set-Cookie header was not present in the response from {Uri}.", uri);
            return new List<string>(0);
        }
        List<string> cookies = setCookie.ToList();
        Logger.LogTrace("GetCookies: received these cookies({Count}): {Cookies}", cookies.Count, cookies.AsString());
        if (!cookies.Any())
        {
            Logger.LogTrace("No cookies returned in the response from {Uri}.", uri);
            return new List<string>(0);
        }
        cookies = cookies
            .Where(c => c.Contains("Domain=.yahoo.com", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Logger.LogTrace("GetCookies: using these cookies({Count}): {Cookies}", cookies.Count, cookies.AsString());
        return cookies;
    }

    private async Task<List<string>> GetEuropeanCookies(CancellationToken ct)
    {
        Logger.LogTrace("GetEuropeanCookies()");
        HttpClient httpClient = HttpClientFactory.CreateClient("cookie");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        Uri uri = new("https://finance.yahoo.com/");
        HttpResponseMessage response = await httpClient.GetAsync(uri, ct).ConfigureAwait(false);

        Uri? redirect = null;
        string? crumb = null, sessionId = null;

        while (response.Headers.Location != null)
        {
            redirect = response.Headers.Location;

            Match match = Regex.Match(redirect.Query, @"gcrumb=(.+?)&", RegexOptions.Compiled);
            if (match.Success)
                crumb = match.Groups[1].Value;

            match = Regex.Match(redirect.Query, @"sessionId=(.*)", RegexOptions.Compiled);
            if (match.Success)
                sessionId = match.Groups[1].Value;

            if (response.Headers.TryGetValues(name: "Set-Cookie", out IEnumerable<string>? tmpCookies))
                httpClient.DefaultRequestHeaders.Add("cookie", tmpCookies);

            Logger.LogTrace("GetEuropeanCookies: requesting {Uri}", redirect);
            response = await httpClient.GetAsync(redirect, ct).ConfigureAwait(false);
        }

        if (redirect == null || crumb == null || sessionId == null)
            return new List<string>(0);

        Dictionary<string, string> form = new()
        {
            {"csrfToken", crumb},
            {"sessionId", sessionId},
            {"originalDoneUrl", "https://finance.yahoo.com/?guccounter=2"},
            {"namespace", "yahoo"},
            {"agree", "agree"}
        };
        using (FormUrlEncodedContent encodedForm = new(form))
        response = await httpClient.PostAsync(redirect, encodedForm, ct).ConfigureAwait(false);
        Logger.LogTrace("GetEuropeanCookies: posted {Uri}", redirect);

        if (response.Headers.Location != null)
        {
            redirect = response.Headers.Location;
            Logger.LogTrace("GetEuropeanCookies: requesting {Uri}", redirect);
            response = await httpClient.GetAsync(redirect, ct).ConfigureAwait(false);
        }

        if (!response.Headers.TryGetValues(name: "Set-Cookie", out IEnumerable<string>? cookies))
            return new List<string>(0);

        Logger.LogTrace("GetEuropeanCookies: received these cookies({Count}): {Cookies}", cookies.Count(), cookies.AsString());
        return cookies.ToList();
    }

    // Make an HTTP GET call which includes the cookie obtained from the previous response.
    private async Task<string> GetCrumb(IEnumerable<string> cookies, CancellationToken ct)
    {
        // "https://query1.finance.yahoo.com/v1/test/getcrumb"
        // "https://query2.finance.yahoo.com/v1/test/getcrumb"
        Uri crumbUri = new("https://query1.finance.yahoo.com/v1/test/getcrumb");
        HttpClient httpClient = HttpClientFactory.CreateClient("crumb");
        httpClient.DefaultRequestHeaders.Add("cookie", cookies);
        using HttpResponseMessage response = await httpClient.GetAsync(crumbUri, ct).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Did not receive crumb from {crumbUri} using cookies.", ex);
        }
        string crumb = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(crumb))
            throw new InvalidOperationException($"Did not receive crumb from {crumbUri} using cookies.");

        Logger.LogTrace("GetCrumb: received crumb {Crumb}", crumb);
        return crumb;
    }
}

internal static class Extensions
{
        internal static string AsString(this IEnumerable<string> cookies) =>
            Environment.NewLine + string.Join(Environment.NewLine, cookies);
}

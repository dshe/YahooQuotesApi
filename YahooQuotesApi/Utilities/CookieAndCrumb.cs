using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
namespace YahooQuotesApi;

public sealed class CookieAndCrumb
{
    private readonly object LockObj = new();
    private readonly ILogger Logger;
    private readonly IHttpClientFactory HttpClientFactory;
    private static Task<(string[], string)>? TheTask; // STATIC!!!

    public CookieAndCrumb(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        Logger = logger;
        HttpClientFactory = httpClientFactory;
    }

    internal async Task<(string[], string)> Get(CancellationToken ct)
    {
        Logger.LogTrace("CookieAndCrumb.Get()");
        // Lazy<Task<T>> does not support cancellation.
        lock (LockObj)
        {
            TheTask ??= GetCookieAndCrumb1(ct); // start the task if not already started
        }
        return await TheTask.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<(string[], string)> GetCookieAndCrumb1(CancellationToken ct)
    {
        try
        {
            string[] cookies = await GetCookies(ct).ConfigureAwait(false);
            if (cookies.Length == 0)
                cookies = await GetEuropeanCookies(ct).ConfigureAwait(false);
            if (cookies.Length == 0)
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

    private async Task<string[]> GetCookies(CancellationToken ct)
    {
        Uri uri = new("https://login.yahoo.com/");

        HttpClient httpClient = HttpClientFactory.CreateClient("HttpV2");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        // This call may result in an error, which may be ignored.
        using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.Headers.TryGetValues(name: "Set-Cookie", out IEnumerable<string>? setCookie))
        {
            Logger.LogTrace("Set-Cookie header was not present in the response from {Uri}.", uri);
            return [];
        }
        string[] cookies = [.. setCookie];
        if (cookies.Length == 0)
        {
            Logger.LogTrace("No cookies returned in the response from {Uri}.", uri);
            return [];
        }
        Logger.LogTrace("GetCookies: received these cookies({Count}): {Cookies}", cookies.Length, cookies.AsString());
        cookies = [.. cookies.Where(c => c.Contains("Domain=.yahoo.com", StringComparison.OrdinalIgnoreCase))];
        Logger.LogTrace("GetCookies: using these cookies({Count}): {Cookies}", cookies.Length, cookies.AsString());
        return cookies;
    }

    private async Task<string[]> GetEuropeanCookies(CancellationToken ct)
    {
        HttpClient httpClient = HttpClientFactory.CreateClient("");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        Logger.LogTrace("GetEuropeanCookies()");
        Uri uri = new(uriString: "https://finance.yahoo.com/");
        //Uri uri = new("https://login.yahoo.com/");
        //Uri uri = new("https://www.yahoo.com/");
        HttpResponseMessage response = await httpClient.GetAsync(uri, ct).ConfigureAwait(false);
        //using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        //var ss = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        Uri redirect = response.Headers.Location ?? throw new InvalidOperationException($"Did not receive redirect location from {uri}.");
        string? crumb = null, sessionId = null;

        while (true)
        {
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
            if (response.Headers.Location == null)
                break;
            redirect = response.Headers.Location;
        }

        if (crumb == null || sessionId == null)
            return [];

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
            return [];

        Logger.LogTrace("GetEuropeanCookies: received these cookies({Count}): {Cookies}", cookies.Count(), cookies.AsString());
        cookies = cookies
            .Where(c => c.StartsWith("A3=", StringComparison.OrdinalIgnoreCase));
        return [.. cookies];
    }

    // Make an HTTP GET call which includes the cookie obtained from the previous response.
    private async Task<string> GetCrumb(IEnumerable<string> cookies, CancellationToken ct)
    {
        HttpClient httpClient = HttpClientFactory.CreateClient("HttpV2");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        httpClient.DefaultRequestHeaders.Add("Cookie", cookies);

        // "https://query1.finance.yahoo.com/v1/test/getcrumb"
        // "https://query2.finance.yahoo.com/v1/test/getcrumb"
        Uri crumbUri = new("https://query2.finance.yahoo.com/v1/test/getcrumb");
        using HttpResponseMessage response = await httpClient.GetAsync(crumbUri, ct).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Did not receive crumb from {crumbUri} using cookies(1).", ex);
        }
        string crumb = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(crumb))
            throw new InvalidOperationException($"Did not receive crumb from {crumbUri} using cookies(2).");
        Logger.LogTrace("GetCrumb: received crumb {Crumb}", crumb);
        return crumb;
    }
}

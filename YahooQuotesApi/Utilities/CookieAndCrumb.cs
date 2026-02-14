using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
namespace YahooQuotesApi;

public sealed class CookieAndCrumb
{
    private static readonly SemaphoreSlim Semaphore = new(1);
    private static ExceptionDispatchInfo? CapturedExceptionInfo;
    private static (string[], string)? CookiesAndCrumb;
    private readonly ILogger Logger;
    private readonly IHttpClientFactory HttpClientFactory;

    public CookieAndCrumb(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        Logger = logger;
        HttpClientFactory = httpClientFactory;
    }

    internal async Task<(string[], string)> Get(CancellationToken ct)
    {
        await Semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            CapturedExceptionInfo?.Throw();
            return CookiesAndCrumb ??= await GetCookieAndCrumb1(ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            CapturedExceptionInfo ??= ExceptionDispatchInfo.Capture(e);
            throw;
        }
        finally 
        {
            Semaphore.Release();
        }
    }

    private async Task<(string[], string)> GetCookieAndCrumb1(CancellationToken ct)
    {
        try
        {
            string[] cookies = await TryGetCookies(ct).ConfigureAwait(false);
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
            Logger.LogCritical(e, "GetCookieAndCrumb1: error");
            throw;
        }
    }

    private async Task<string[]> TryGetCookies(CancellationToken ct)
    {
        HttpClient httpClient = HttpClientFactory.CreateClient("HttpV2");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        // This call may result in an error, which may be ignored.
        //fc.yahoo.com, query2.finance.yahoo.com, query1.finance.yahoo.com
        //Uri uri = new("https://login.yahoo.com/");
        Uri uri = new("https://finance.yahoo.com/");
        Logger.LogTrace("GetCookies: requesting {Uri}", uri);
        using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.Headers.TryGetValues(name: "Set-Cookie", out IEnumerable<string>? setCookie))
        {
            Logger.LogTrace("Set-Cookie header was not present in the response from {Uri}.", uri);
            return [];
        }
        string[] cookies = [.. setCookie];
        if (cookies.Length == 0)
        {
            Logger.LogTrace("GetCookies: No cookies returned in the response from {Uri}.", uri);
            return [];
        }
        Logger.LogTrace("GetCookies: received these cookies({Count}): {Cookies}", cookies.Length, cookies.AsString());
        cookies = [.. cookies.Where(c => c.Contains("Domain=.yahoo.com", StringComparison.OrdinalIgnoreCase) && c.StartsWith('A'))];
        // should be no cookies here if the user is in Europe
        Logger.LogTrace("GetCookies: filtered cookies({Count}): {Cookies}", cookies.Length, cookies.AsString());
        return cookies;
    }

    private async Task<string[]> GetEuropeanCookies(CancellationToken ct)
    {
        HttpClient httpClient = HttpClientFactory.CreateClient("");

        // Thank you Alexander Gnauck!
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        Uri uri = new(uriString: "https://finance.yahoo.com/");
        //Uri uri = new("https://login.yahoo.com/");
        //Uri uri = new("https://yahoo.com/");
        Logger.LogTrace("GetEuropeanCookies: requesting1 {Uri}", uri);
        HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

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

            Logger.LogTrace("GetEuropeanCookies: requesting2 {Uri}", redirect);
            response = await httpClient.GetAsync(redirect, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
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

        Logger.LogTrace("GetEuropeanCookies: posting {Uri}", redirect);
        using FormUrlEncodedContent encodedForm = new(form);
        response = await httpClient.PostAsync(redirect, encodedForm, ct).ConfigureAwait(false);

        if (response.Headers.Location != null)
        {
            redirect = response.Headers.Location;
            Logger.LogTrace("GetEuropeanCookies: requesting {Uri}", redirect);
            response = await httpClient.GetAsync(redirect, HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false);
        }

        if (!response.Headers.TryGetValues(name: "Set-Cookie", out IEnumerable<string>? setCookie))
            return [];
        string[] cookies = [.. setCookie];
        Logger.LogTrace("GetEuropeanCookies: received these cookies({Count}): {Cookies}", cookies.Length, cookies.AsString());

        cookies = [.. cookies.Where(c => c.StartsWith("A3=", StringComparison.OrdinalIgnoreCase))];
        Logger.LogTrace("GetEuropeanCookies: filtered cookies({Count}): {Cookies}", cookies.Length, cookies.AsString());
        return cookies;
    }

    // Make an HTTP GET call which includes the cookie obtained from the previous response.
    private async Task<string> GetCrumb(IEnumerable<string> cookies, CancellationToken ct)
    {
        await Task.Delay(500, ct).ConfigureAwait(false);

        HttpClient httpClient = HttpClientFactory.CreateClient("HttpV2");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        httpClient.DefaultRequestHeaders.Add("Cookie", cookies);

        // "https://query1.finance.yahoo.com/v1/test/getcrumb"
        // "https://query2.finance.yahoo.com/v1/test/getcrumb"
        Uri crumbUri = new("https://query1.finance.yahoo.com/v1/test/getcrumb");
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
        Logger.LogTrace("GetCrumb: received crumb {Crumb}.", crumb);
        return crumb;
    }
}

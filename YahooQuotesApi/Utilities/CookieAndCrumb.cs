using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
namespace YahooQuotesApi;

public sealed class CookieAndCrumb(ILogger logger, IHttpClientFactory httpClientFactory)
{
    private static readonly SemaphoreSlim _semaphore = new(1);
    private static (string[], string)? _cookiesAndCrumbStatic;
    private readonly ILogger _logger = logger;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    internal async Task<(string[], string)> GetAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _cookiesAndCrumbStatic ??= await GetCookieAndCrumbAsync(ct).ConfigureAwait(false);
        }
        finally 
        {
            _semaphore.Release();
        }
    }

    private async Task<(string[], string)> GetCookieAndCrumbAsync(CancellationToken ct)
    {
        try
        {
            string[] cookies = await TryGetCookiesAsync(ct).ConfigureAwait(false);
            if (cookies.Length == 0)
                cookies = await GetEuropeanCookiesAsync(ct).ConfigureAwait(false);
            if (cookies.Length == 0)
            {
                _logger.LogCritical("No cookies found.");
                throw new InvalidOperationException("No cookies found.");
            }
            string crumb = await GetCrumbAsync(cookies, ct).ConfigureAwait(false);
            return (cookies, crumb);
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "GetCookieAndCrumb1: error");
            throw;
        }
    }

    private async Task<string[]> TryGetCookiesAsync(CancellationToken ct)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient("");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        // This call may result in an error, which may be ignored.
        //fc.yahoo.com, query2.finance.yahoo.com, query1.finance.yahoo.com
        //Uri uri = new("https://login.yahoo.com/");
        Uri uri = new("https://finance.yahoo.com/");
        _logger.LogTrace("GetCookies: requesting {Uri}", uri);

        // fails here
        using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.Headers.TryGetValues(name: "Set-Cookie", out IEnumerable<string>? setCookie))
        {
            _logger.LogTrace("Set-Cookie header was not present in the response from {Uri}.", uri);
            return [];
        }
        string[] cookies = [.. setCookie];
        if (cookies.Length == 0)
        {
            _logger.LogTrace("GetCookies: No cookies returned in the response from {Uri}.", uri);
            return [];
        }
        _logger.LogTrace("GetCookies: received these cookies({Count}): {Cookies}", cookies.Length, cookies.AsString());
        cookies = [.. cookies.Where(c => c.Contains("Domain=.yahoo.com", StringComparison.OrdinalIgnoreCase) && c.StartsWith('A'))];
        // should be no cookies here if the user is in Europe
        _logger.LogTrace("GetCookies: filtered cookies({Count}): {Cookies}", cookies.Length, cookies.AsString());
        return cookies;
    }

    private async Task<string[]> GetEuropeanCookiesAsync(CancellationToken ct)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient("");

        // Thank you Alexander Gnauck!
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        Uri uri = new(uriString: "https://finance.yahoo.com/");
        //Uri uri = new("https://login.yahoo.com/");
        //Uri uri = new("https://yahoo.com/");
        _logger.LogTrace("GetEuropeanCookies: requesting1 {Uri}", uri);
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

            _logger.LogTrace("GetEuropeanCookies: requesting2 {Uri}", redirect);
            response = await httpClient.GetAsync(redirect, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (response.Headers.Location is null)
                break;
            redirect = response.Headers.Location;
        }

        if (crumb is null || sessionId is null)
            return [];

        Dictionary<string, string> form = new()
        {
            {"csrfToken", crumb},
            {"sessionId", sessionId},
            {"originalDoneUrl", "https://finance.yahoo.com/?guccounter=2"},
            {"namespace", "yahoo"},
            {"agree", "agree"}
        };

        _logger.LogTrace("GetEuropeanCookies: posting {Uri}", redirect);
        using FormUrlEncodedContent encodedForm = new(form);
        response = await httpClient.PostAsync(redirect, encodedForm, ct).ConfigureAwait(false);

        if (response.Headers.Location is not null)
        {
            redirect = response.Headers.Location;
            _logger.LogTrace("GetEuropeanCookies: requesting {Uri}", redirect);
            response = await httpClient.GetAsync(redirect, HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false);
        }

        if (!response.Headers.TryGetValues(name: "Set-Cookie", out IEnumerable<string>? setCookie))
            return [];
        string[] cookies = [.. setCookie];
        _logger.LogTrace("GetEuropeanCookies: received these cookies({Count}): {Cookies}", cookies.Length, cookies.AsString());

        cookies = [.. cookies.Where(c => c.StartsWith("A3=", StringComparison.OrdinalIgnoreCase))];
        _logger.LogTrace("GetEuropeanCookies: filtered cookies({Count}): {Cookies}", cookies.Length, cookies.AsString());
        return cookies;
    }

    // Make an HTTP GET call which includes the cookie obtained from the previous response.
    private async Task<string> GetCrumbAsync(IEnumerable<string> cookies, CancellationToken ct)
    {
        await Task.Delay(500, ct).ConfigureAwait(false);

        HttpClient httpClient = _httpClientFactory.CreateClient("");
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
        _logger.LogTrace("GetCrumb: received crumb {Crumb}.", crumb);
        return crumb;
    }
}

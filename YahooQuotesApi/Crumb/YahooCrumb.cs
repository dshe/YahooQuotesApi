using System.Net.Http;

namespace YahooQuotesApi.Crumb;

public sealed class YahooCrumb : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim SemaphoreSlim = new(1, 1);
    private (List<string>, string) CookieAndCrumb;

    public YahooCrumb(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public async Task<(List<string>, string)> GetCookieAndCrumb(CancellationToken ct)
    {
        await SemaphoreSlim.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (CookieAndCrumb == default)
                CookieAndCrumb = await RetrieveCookieAndCrumb(ct).ConfigureAwait(false);
            return CookieAndCrumb;
        }
        finally
        {
            SemaphoreSlim.Release();
        }
    }

    private async Task<(List<string>, string)> RetrieveCookieAndCrumb(CancellationToken ct)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient("crumb");

        // This call results in a 404 error,
        // but we just need it to extract set-cookie from response headers
        // which is then used in the subsequent calls
        //Uri fcUrl = new("https://fc.yahoo.com/"); // was extremely slow
        Uri setCookieUrl = new("https://login.yahoo.com");
        using HttpResponseMessage setCookieResponse = await httpClient.GetAsync(setCookieUrl, ct).ConfigureAwait(false);

        if (!setCookieResponse.Headers.TryGetValues("Set-Cookie", out var setCookie))
            throw new InvalidOperationException("Set-Cookie header was not present in the response from " + setCookieUrl);
        List<string> cookies = setCookie.Where(c => c.StartsWith("A3=d", StringComparison.OrdinalIgnoreCase)).ToList();

        // Now make an HTTP GET call, by including the obtained cookie from the previous response headers.
        // This call will retrieve the crumb value.
        httpClient.DefaultRequestHeaders.Add("cookie", cookies);
        Uri crumbUrl = new("https://query2.finance.yahoo.com/v1/test/getcrumb");
        using HttpResponseMessage crumbResponse = await httpClient.GetAsync(crumbUrl, ct).ConfigureAwait(false);
        crumbResponse.EnsureSuccessStatusCode(); // error here

        string crumb = await crumbResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(crumb))
            throw new HttpRequestException($"Could not generate crumb from {crumbUrl} for cookie {cookies.First(x => x.StartsWith("A3", StringComparison.OrdinalIgnoreCase))}");

        // Cache the cookie and crumb values to use with further requests.
        // httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
        // Example: https://query2.finance.yahoo.com/v7/finance/quote?symbols=TSLA&crumb=[crumb]
        return (cookies, crumb);
    }

    public void Dispose() => SemaphoreSlim.Dispose();
}
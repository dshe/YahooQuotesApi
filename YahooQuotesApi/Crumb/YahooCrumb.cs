using System.Net.Http;

namespace YahooQuotesApi.Crumb;

public class YahooCrumb
{
    private const string YahooFcUrl = "https://fc.yahoo.com/";
    private const string YahooGetCrumbUrl = "https://query2.finance.yahoo.com/v1/test/getcrumb";
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly object _lock = new();
    private (IEnumerable<string>, string) _storedCookieAndCrumb;
    private (IEnumerable<string>, string) StoredCookieAndCrumb
    {
        get
        {
            lock (_lock)
            {
                return _storedCookieAndCrumb;
            }
        }
        set
        {
            lock (_lock)
            {
                _storedCookieAndCrumb = value;
            }
        }
    }

    public YahooCrumb(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<(IEnumerable<string>, string)> GetCookieAndCrumb(CancellationToken ct)
    {
        // Get from cache
        if (StoredCookieAndCrumb != default((IEnumerable<string>, string)))
            return StoredCookieAndCrumb;

        // Not in cache, request it from Yahoo
#pragma warning disable CA2000 // Dispose objects before losing scope, HttpClientFactory takes care of managing HttpClient instances
        HttpClient httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 

        // FC
        Uri fcUrl = new(YahooFcUrl);

        using HttpResponseMessage fcResponse = await httpClient.GetAsync(fcUrl, ct).ConfigureAwait(false);

        if (!fcResponse.Headers.TryGetValues("Set-Cookie", out var cookie))
            throw new InvalidOperationException("Set-Cookie header was not present in the response from " + fcUrl);

        // Crumb
        Uri crumbUrl = new(YahooGetCrumbUrl);

        httpClient.DefaultRequestHeaders.Add("cookie", cookie);

        using HttpResponseMessage response = await httpClient.GetAsync(crumbUrl, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string crumb = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(crumb))
            throw new HttpRequestException($"Could not generate crumb from {YahooGetCrumbUrl} for cookie {cookie.First(x => x.StartsWith("A3", StringComparison.OrdinalIgnoreCase))}");

        StoredCookieAndCrumb = (cookie, crumb);

        return StoredCookieAndCrumb;
    }
}
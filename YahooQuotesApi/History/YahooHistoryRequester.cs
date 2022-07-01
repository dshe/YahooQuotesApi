using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
namespace YahooQuotesApi;

internal class YahooHistoryRequester
{
    private readonly ILogger Logger;
    private readonly HttpClient HttpClient;

    internal YahooHistoryRequester(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        Logger = logger;
        HttpClient = httpClientFactory.CreateClient("history");
    }

    internal async Task<HttpResponseMessage> Request(string url, CancellationToken ct)
    {
        if (Logger.IsEnabled(LogLevel.Information))
            Logger.LogInformation("{Url}", url);
        Uri uri = new(url);
        //HttpResponseMessage response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        HttpResponseMessage response = await HttpClient.GetAsync(uri, ct).ConfigureAwait(false);
        //Stream stream = await HttpClient.GetStreamAsync(uri, ct).ConfigureAwait(false);
        // x-yahoo-request-id
        // y-rid
        return response;
    }
}


using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi;

public sealed class YahooModules
{
    private readonly ILogger Logger;
    private readonly IHttpClientFactory HttpClientFactory;

    public YahooModules(YahooQuotesBuilder builder, IHttpClientFactory factory)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        Logger = builder.Logger;
        HttpClientFactory = factory;
    }

    internal async Task<Result<JsonProperty[]>> GetModulesAsync(string symbol, string[] modules, CancellationToken ct)
    {
        if (!Symbol.TryCreate(symbol, out var sym) || sym.IsCurrency)
            throw new ArgumentException($"Invalid symbol: {sym.Name}.");

        if (!modules.Any())
            throw new ArgumentException("No modules indicated.");
        if (modules.Any(x => string.IsNullOrEmpty(x)))
            throw new ArgumentException("Invalid module: \"\"");
        string? invalidModule = modules.FirstOrDefault(x => x.Contains(',', StringComparison.OrdinalIgnoreCase));
        if (invalidModule != null)
            throw new ArgumentException($"Invalid module: {invalidModule}.");
        var dups = modules.GroupBy(x => x).Where(x => x.Count() > 1);
        if (dups.Any())
            throw new ArgumentException($"Duplicate module(s): \'{string.Join(", ", dups)}\'.");

        Result<JsonProperty[]> result = await Produce(symbol, modules, ct).ConfigureAwait(false);
        return result;
    }

    private async Task<Result<JsonProperty[]>> Produce(string symbol, string[] modulesRequested, CancellationToken ct)
    {
        HttpClient httpClient = HttpClientFactory.CreateClient("modules");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        Uri uri = GetUri(symbol, modulesRequested);

        // Don't use httpClient.GetFromJsonAsync<JsonDocument>() because it does not allow reading json error messages like NotFound.
        using HttpResponseMessage response = await httpClient.GetAsync(uri, ct).ConfigureAwait(false);
        //if (response.StatusCode != HttpStatusCode.NotFound) // NotFound will return a json error message.
        //    response.EnsureSuccessStatusCode();
        using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        JsonDocument jsonDocument = await JsonDocument.ParseAsync(stream, default, ct).ConfigureAwait(false);

        return GetModules(modulesRequested, jsonDocument);
    }

    private Uri GetUri(string symbol, string[] modules)
    {
        const string address = "https://query2.finance.yahoo.com/v11/finance/quoteSummary";
        string url = $"{address}/{symbol}?modules={string.Join(",", modules)}";

        Logger.LogInformation("{Url}", url);

        return new Uri(url);
    }

    private static Result<JsonProperty[]> GetModules(string[] modulesRequested, JsonDocument jsonDocument)
    {
        if (!jsonDocument.RootElement.TryGetProperty("quoteSummary", out JsonElement quoteSummary))
            throw new InvalidDataException("quoteSummary");
        if (!quoteSummary.TryGetProperty("error", out JsonElement error))
            throw new InvalidDataException("error");
        if (error.ValueKind is not JsonValueKind.Null)
        {
            if (error.TryGetProperty("description", out JsonElement property))
            {
                string? description = property.GetString();
                if (description is not null)
                    return Result<JsonProperty[]>.Fail(description);
            }
            return Result<JsonProperty[]>.Fail(error.ToString());
        }

        if (!quoteSummary.TryGetProperty("result", out JsonElement result))
            throw new InvalidDataException("result");
        JsonElement[] items = result.EnumerateArray().ToArray();
        if (items.Length != 1)
            throw new InvalidDataException($"Error requesting YahooModules list.");
        JsonElement item = items.Single();
        JsonProperty[] modules = item.EnumerateObject().ToArray();

        return VerifyModules(modulesRequested, modules);
    }

    private static Result<JsonProperty[]> VerifyModules(string[] moduleNamesRequested, JsonProperty[] modules)
    {
        string[] moduleNames = modules.Select(module => module.Name).ToArray();

        string[] missingModules = moduleNamesRequested.Where(n => !moduleNames.Contains(n, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (missingModules.Any())
            return Result<JsonProperty[]>.Fail($"Invalid module(s): \'{string.Join(", ", missingModules)}\'.");

        string[] extraModules = moduleNames.Where(n => !moduleNamesRequested.Contains(n, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (extraModules.Any())
            return Result<JsonProperty[]>.Fail($"Extra module(s): \'{string.Join(", ", extraModules)}\'.");

        if (moduleNamesRequested.Length != modules.Length)
            return Result<JsonProperty[]>.Fail($"Invalid modules.");

        return modules.ToResult();
    }
}

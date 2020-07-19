using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Text;

/*
https://www.bankofengland.co.uk/statistics/exchange-rates
The data represents indicative middle market(mean of spot buying and selling)
rates as observed by the Bank's Foreign Exchange Desk in the London interbank market around 4pm.
BOE uses non-standard currency symbols.
These BoE currency rates are in units per USD(USDJPY = 100).
*/

namespace YahooQuotesApi
{
	internal class BoeCurrencyHistory
	{
        private readonly static LocalTime SpotTime = new LocalTime(16, 0, 0);
        private readonly static DateTimeZone TimeZone =  DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/London")!;
        private readonly static LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");
        private readonly static Dictionary<string, (string code, string name)> SymbolDictionary =
            new Dictionary<string, (string code, string name)>(StringComparer.OrdinalIgnoreCase)
        {
            { "USD", ("USD", "US Dollar") },
            { "AUD", ("ADD", "Australian Dollar") },
            { "CAD", ("CDD", "Canadian Dollar") },
            { "DKK", ("DKD", "Danish Krone") },
            { "EUR", ("ERD", "Euro") },
            { "HKD", ("HDD", "Hong Kong Dollar") },
            { "JPY", ("JYD", "Japanese Yen") },
            { "NZD", ("NDD", "New Zealand Dollar") },
            { "NOK", ("NKD", "Norwegian Krone") },
            { "CHF", ("SFD", "Swiss Franc") },
            { "SGD", ("SGD", "Singapore Dollar") },
            { "SEK", ("SKD", "Swedish Krona") },
            { "GBP", ("GBD", "British Pound") },
            { "SAR", ("SRD", "Saudi Riyal") },
            { "TWD", ("TWD", "Taiwan Dollar") },
            { "ZAR", ("ZRD", "South African Rand") },
            //{ "CYP", ("BK24", "Cyprus Pound") }, // 1879-2007
            { "CZK", ("BK27", "Czech Koruna") },
            //{ "EEK", ("BK32", "Estonian Kroon") }, // 1928–1940, 1992–2011
            { "HUF", ("BK35", "Hungarian Forint") },
            //{ "LTL", ("BK38", "Lithuanian Litas") }, // 2002
            //{ "LVL", ("BK43", "Latvian Lats") }, // 2014
            //{ "MTL", ("BK46", "Maltese Lira") }, // 1972-2007
            { "PLN", ("BK49", "Polish Zloty") },
            //{ "SIT", ("BK54", "Slovenian Tolar") }, // 2007
            //{ "SKK", ("BK57", "Slovak Koruna") }, // 1993-2008
            { "INR", ("BK64", "Indian Ruppee") },
            { "ILS", ("BK65", "Israeli Shekel") },
            { "MYR", ("BK66", "Malaysian Ringgit") },
            { "RUB", ("BK69", "Russian Ruble") },
            { "THB", ("BK72", "Thai Baht") },
            { "CNY", ("BK73", "Chinese Yuan") },
            { "KRW", ("BK74", "Korean Wan") },
            { "TRY", ("BK75", "Turkish Lira") },
            { "BRL", ("B8KL", "Brazilian Real") }
        };

        internal static Dictionary<string, string> Symbols { get; } = SymbolDictionary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.name);
        private readonly ILogger Logger;
        private readonly HttpClient HttpClient;
        private readonly string DateFromString = "01/Jan/1963";

        internal BoeCurrencyHistory(ILogger? logger, HttpClient? httpClient = null, LocalDate? dateFrom = null)
        {
            Logger = logger ?? NullLogger.Instance;
            HttpClient = httpClient ?? new HttpClient();
            var date = dateFrom ?? LocalDate.MinIsoValue;
            if (date.At(SpotTime).InZoneStrictly(TimeZone).ToInstant() > Utility.Clock.GetCurrentInstant())
                throw new ArgumentException("DateFrom > now");
            DateFromString = date == LocalDate.MinIsoValue ? "01/Jan/1963" : date.ToString("dd/MMM/yyyy", CultureInfo.InvariantCulture);
        }

        internal async Task<List<RateTick>> GetRatesAsync(string symbol, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(symbol))
                throw new ArgumentNullException(nameof(symbol));
            symbol = symbol.ToUpper();
            if (symbol == "USD")
                throw new ArgumentException("Invalid symbol: USD is base.");
            if (!SymbolDictionary.TryGetValue(symbol, out var info))
                throw new ArgumentException($"BOE unsupported currency: {symbol}.");
            var xdoc = await GetXDoc(info.code, ct).ConfigureAwait(false);
            return CreateList(xdoc);
        }

        private async Task<XDocument> GetXDoc(string boeSymbol, CancellationToken ct)
        {
            var url = $"http://www.BankOfEngland.co.uk/boeapps/iadb/fromshowcolumns.asp?CodeVer=new&xml.x=yes&Datefrom={DateFromString}&Dateto=now&SeriesCodes=XUDL{boeSymbol}";
            // Datefrom={dateFrom}&Dateto=now is mandatory; the earlies date seems to be 1963
            // SeriesCodes=XUDL{boeSymbol}
            Logger.LogInformation($"{url}.");
            var response = await HttpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType.MediaType != "text/xml")
                throw new NotSupportedException($"XML not returned from:\r\n{url}");
            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return XDocument.Load(stream);
        }

        private static List<RateTick> CreateList(XDocument xdoc) =>
            xdoc.Root.Descendants()
                .Where(x => x.Attribute("TIME") != null)
                .Select(row => new RateTick(ParseDate(row), ParseRate(row)))
                .ToList();

        private static ZonedDateTime ParseDate(XElement row)
        {
            var dateStr = row.Attribute("TIME").Value; // no daylight savings, UTC already
            var result = DatePattern.Parse(dateStr);
            if (!result.Success)
                throw new Exception($"Could not convert {dateStr} to LocalDate.", result.Exception);
            return result.Value.At(SpotTime).InZoneStrictly(TimeZone);
        }
        
        private static double ParseRate(XElement row) => double.Parse(row.Attribute("OBS_VALUE").Value);
    }
}



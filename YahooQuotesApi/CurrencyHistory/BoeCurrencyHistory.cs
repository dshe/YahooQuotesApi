using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

/*
https://www.bankofengland.co.uk/statistics/exchange-rates
The data represents indicative middle market(mean of spot buying and selling)
rates as observed by the Bank's Foreign Exchange Desk in the London interbank market around 4pm.
BOE uses non-standard currency symbols so we need to find them.
These BoE currency rates are in units per USD(USDJPY = 100).
*/

namespace YahooQuotesApi
{
	internal class BoeCurrencyHistory
	{
        internal static LocalTime SpotTime { get; } = new LocalTime(16, 0, 0);
        internal static DateTimeZone TimeZone { get; } = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/London");
        private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");
        internal static readonly IReadOnlyDictionary<string, (string code, string name)> Symbols = new Dictionary<string, (string code, string name)>(StringComparer.OrdinalIgnoreCase)
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

            { "CYP", ("BK24", "Cyprus Pound") },
            { "CZK", ("BK27", "Czech Koruna") },
            { "EEK", ("BK32", "Estonian Kroon") },
            { "HUF", ("BK35", "Hungarian Forint") },
            { "LTL", ("BK38", "Lithuanian Litas") },
            { "LVL", ("BK43", "Latvian Lats") },
            { "MTL", ("BK46", "Maltese Lira") },
            { "PLN", ("BK49", "Polish Zloty") },
            { "SIT", ("BK54", "Slovenian Tolar") },
            { "SKK", ("BK57", "Slovak Koruna") },

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

        private string DateFrom = "01/Jan/1963";
        private readonly ILogger Logger;
        private readonly HttpClient HttpClient;

        internal BoeCurrencyHistory(ILogger logger, HttpClient httpClient)
        {
            Logger = logger;
            HttpClient = httpClient;
        }

        internal void SetStartDate(LocalDate start)
        {
            if (start.At(SpotTime).InZoneStrictly(TimeZone).ToInstant() > Utility.Clock.GetCurrentInstant())
                throw new ArgumentException("start > now");
            DateFrom = start == LocalDate.MinIsoValue ? "01/Jan/1963" : start.ToString("dd/MMM/yyyy", CultureInfo.InvariantCulture);
        }

        internal async Task<List<RateTick>> Retrieve(string symbol, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(symbol))
                throw new ArgumentException(nameof(symbol));

            if (string.Compare(symbol, "USD", StringComparison.InvariantCultureIgnoreCase) == 0)
                throw new ArgumentException("Invalid symbol: USD is base.");

            if (!Symbols.TryGetValue(symbol, out var info))
                throw new ArgumentException($"BOE unsupported currency: {symbol}.");

            var xdoc = await GetXDoc(info.code, ct).ConfigureAwait(false);
            return CreateList(xdoc);
        }

        private async Task<XDocument> GetXDoc(string boeSymbol, CancellationToken ct)
        {
            var url = $"http://www.BankOfEngland.co.uk/boeapps/iadb/fromshowcolumns.asp?CodeVer=new&xml.x=yes&Datefrom={DateFrom}&Dateto=now&SeriesCodes=XUDL{boeSymbol}";
            // Datefrom={dateFrom}&Dateto=now is mandatory; the earlies date is 1963
            // SeriesCodes=XUDL{boeSymbol}
            Logger.LogInformation($"{url}.");
            var response = await HttpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType.MediaType != "text/xml")
                throw new NotSupportedException($"XML not returned from:\r\n{url}");
            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return XDocument.Load(stream);
        }

        private static List<RateTick> CreateList(XDocument xdoc)
        {
            var list = new List<RateTick>();
            var rows = xdoc.Root.Descendants().Where(x => x.Attribute("TIME") != null);
            foreach (var row in rows)
            {
                var rate = ParseRate(row);
                var date = ParseDate(row);
                list.Add(new RateTick(date, rate));
            }
            return list;
        }

        private static Instant ParseDate(XElement row)
        {
            var dateStr = row.Attribute("TIME").Value; // no daylight savings, UTC already
            var result = DatePattern.Parse(dateStr);
            if (!result.Success)
                throw new Exception($"Could not convert {dateStr} to LocalDate.", result.Exception);
            return result.Value.At(SpotTime).InZoneStrictly(TimeZone).ToInstant();
        }
        
        private static double ParseRate(XElement row) => double.Parse(row.Attribute("OBS_VALUE").Value);
    }
}



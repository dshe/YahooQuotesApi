using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace YahooQuotesApi
{
	internal class BoeCurrencyHistory
	{
        private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");
        private static readonly HttpClient client = new HttpClient();
        static BoeCurrencyHistory()
        {
            ServicePointManager.DefaultConnectionLimit = 16;
            ServicePointManager.UseNagleAlgorithm = false;
            client.Timeout = TimeSpan.FromSeconds(30); // default IS 100 seconds
        }
        private readonly ILogger Logger;
        internal BoeCurrencyHistory(ILogger logger) => Logger = logger;

        // this is the complete list of currencies from Bank of England; BOE uses non-standard currency symbols so we need to find them
        private string GetBoeSymbol(string symbol) => symbol switch
        {
            "USD" => "USD",
            "AUD" => "ADD",
			"CAD" => "CDD",
			"DKK" => "DKD",
			"EUR" => "ERD",
			"HKD" => "HDD",
            "JPY" => "JYD",
			"NZD" => "NDD",
			"NOK" => "NKD",
			"CHF" => "SFD",
			"SGD" => "SGD",
			"SEK" => "SKD",
			"GBP" => "GBD",
			"SAR" => "SRD",
			"TWD" => "TWD",
			"ZAR" => "ZRD",

			"CYP" => "BK24",
			"CZK" => "BK27",
			"EEK" => "BK32",
			"HUF" => "BK35",
			"LTL" => "BK38",
			"LVL" => "BK43",
			"MTL" => "BK46",
			"PLN" => "BK49",
			"SIT" => "BK54",
			"SKK" => "BK57",
			
            "INR" => "BK64",
			"ILS" => "BK65",
			"MYR" => "BK66",
			"RUB" => "BK69",
			"THB" => "BK72",
			"CNY" => "BK73",
			"KRW" => "BK74",
			"TRY" => "BK75",
			
            "BRL" => "B8KL", // monthly
            
            _ => throw new ArgumentException($"Unknown BOE currency: {symbol}.")
        };

        // These BoE currency values are in units per USD(USDJPY = 100)
        internal async Task<List<RateTick>> Retrieve(string symbol, LocalDate start, LocalDate end)
        {
            if (symbol == "USD")
                throw new ArgumentException("Invalid symbol: USD.");
            var boeSymbol = GetBoeSymbol(symbol);
            var xdoc = await GetXDoc(boeSymbol, start, end);
            return CreateList(xdoc);
        }

        private async Task<XDocument> GetXDoc(string boeSymbol, LocalDate start, LocalDate end)
        {
            var dateFrom = start.ToString("dd/MMM/yyyy", CultureInfo.InvariantCulture);
            var dateTo = end == LocalDate.MaxIsoValue ? "now" : end.ToString("dd/MMM/yyyy", CultureInfo.InvariantCulture);
            var url = $"http://www.BankOfEngland.co.uk/boeapps/iadb/fromshowcolumns.asp?CodeVer=new&xml.x=yes&Datefrom={dateFrom}&Dateto={dateTo}&SeriesCodes=XUDL{boeSymbol}";
            // Datefrom={dateFrom}&Dateto=now is mandatory
            // SeriesCodes=XUDL{boeSymbol}
            Logger.LogInformation($"{url}.");
            var stream = await client.GetStreamAsync(url).ConfigureAwait(false);
            return XDocument.Load(stream);
        }

        private static List<RateTick> CreateList(XDocument xdoc)
        {
            var list = new List<RateTick>();
            var rows = xdoc.Root.Descendants().Where(x => x.Attribute("TIME") != null);
            foreach (var row in rows)
            {
                var rate = ParseRate(row);
                if (rate == 0m)
                    continue;
                var date = ParseDate(row);
                list.Add(new RateTick(date, rate));
            }
            return list;
        }

        private static LocalDate ParseDate(XElement row)
        {
            var dateStr = row.Attribute("TIME").Value; // no daylight savings, UTC already
            var result = DatePattern.Parse(dateStr);
            if (result.Success)
                return result.Value;
            throw new Exception($"Could not convert {dateStr} to LocalDate.", result.Exception);
        }
        
        private static decimal ParseRate(XElement row) => decimal.Parse(row.Attribute("OBS_VALUE").Value);
    }
}



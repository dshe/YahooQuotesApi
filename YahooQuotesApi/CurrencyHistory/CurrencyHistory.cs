using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    internal class CurrencyHistory
    {
        private static readonly string[] NumeratorCurrencySymbols = { "CAD", "CHF", "CZK", "DKK", "HKD", "HUF", "ILS", "JPY", "MXN", "NOK", "PLN", "SEK", "SGD" };
        internal static bool IsNumeratorSymbol(string symbol) => NumeratorCurrencySymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase);
        private readonly BoeCurrencyHistory BoeCurrency;
        private readonly Dictionary<string, Currency> CurrencyList = new Dictionary<string, Currency>(StringComparer.OrdinalIgnoreCase);
        private void Add(string symbol, string name) => CurrencyList.Add(symbol, new Currency(symbol, name));
        private readonly ILogger Logger;
        private LocalDate Start = LocalDate.MinIsoValue, End = LocalDate.MaxIsoValue;
        public IReadOnlyList<string> Symbols { get; }

        internal CurrencyHistory() : this(NullLogger<CurrencyHistory>.Instance) { }
        internal CurrencyHistory(ILogger<CurrencyHistory> logger)
        {
            Logger = logger;
            BoeCurrency = new BoeCurrencyHistory(logger);

            Add("USD", "US Dollar");
            Add("AUD", "Australian Dollar");
            Add("CAD", "Canadian Dollar");
            Add("DKK", "Danish Krone");
            Add("EUR", "Euro");
            Add("HKD", "Hong Kong Dollar");
            Add("JPY", "Japanese Yen");
            Add("NZD", "New Zealand Dollar");
            Add("NOK", "Norwegian Krone");
            Add("CHF", "Swiss Franc");
            Add("SGD", "Singapore Dollar");
            Add("SEK", "Swedish Krona");
            Add("GBP", "British Pound");
            Add("SAR", "Saudi Riyal");
            Add("TWD", "Taiwan Dollar");
            Add("ZAR", "South African Rand");
			//Add("CYP", "Cyprus Pound");
			//Add("CZK", "Czech Koruna");
			//Add("EEK", "Estonian Kroon");
			//Add("HUF", "Hungarian Forint");
			//Add("LTL", "Lithuanian Litas");
			//Add("LVL", "Latvian Lats");
			//Add("MTL", "Maltese Lira");
			//Add("PLN", "Polish Zloty");
			//Add("SIT", "Slovenian Tolar");
			//Add("SKK", "Slovak Koruna");
			Add("INR", "Indian Ruppee");
            Add("ILS", "Israeli Shekel");
            Add("MYR", "Malaysian Ringgit");
            Add("RUB", "Russian Ruble");
            Add("THB", "Thai Baht");
            Add("CNY", "Chinese Yuan");
            Add("KRW", "Korean Wan");
            Add("TRY", "Turkish Lira");
            //Add("BRL", "Brazilian Real"); // only monthly
            //Add("ZZZ", "Test Symbol");

            var symbols = CurrencyList.Keys.Where(sym => sym != "USD").OrderBy(sym => sym).ToList();
            symbols.Insert(0, "USD"); // put USD on top
            Symbols = symbols;
        }

        public CurrencyHistory Period(LocalDate start, LocalDate end)
        {
            if (start.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant() > Utility.Clock.GetCurrentInstant())
                throw new ArgumentException("start > now");
            if (start > end)
                throw new ArgumentException("start > end");
            Start = start;
            End = end;
            return this;
        }
        public CurrencyHistory Period(LocalDate start) => Period(start, LocalDate.MaxIsoValue);
        public CurrencyHistory Period(int days) => Period(Utility.Clock.GetCurrentInstant().Minus(Duration.FromDays(days)).InUtc().Date);

        public async Task<List<RateTick>?> GetRatesAsync(string symbol) // USDJPY=X 80
        {
            if (symbol.Length != 8 || !symbol.EndsWith("=X") || !symbol.Substring(0, 6).All(x => char.IsLetter(x) && char.IsUpper(x)))
                throw new ArgumentException($"Invalid currency symbol format: {symbol}.");
            string symbol1 = symbol.Substring(0, 3), symbol2 = symbol.Substring(3, 3);
            if (symbol1 == symbol2)
                throw new ArgumentException($"Invalid currency symbol: {symbol}.");
            if (!Symbols.Contains(symbol1) || !Symbols.Contains(symbol2))
                return null; //symbol not supported
            return await GetComponents(symbol1, symbol2).ConfigureAwait(false);
        }

        private async Task<List<RateTick>> GetComponents(string symbol1, string symbol2)
        {
            var task1 = (symbol1 != "USD") ? Get(symbol1) : null;
            var task2 = (symbol2 != "USD") ? Get(symbol2) : null;
            var currency1 = (task1 != null) ? await task1.ConfigureAwait(false) : null;
            var currency2 = (task2 != null) ? await task2.ConfigureAwait(false) : null;

            if (currency1 == null)
                return currency2!.Rates;
            if (currency2 == null)
                return currency1.Rates.Select(r => new RateTick(r.Date, 1m / r.Rate)).ToList(); // invert
            var comboRates = new List<RateTick>();
            foreach (var tick in currency2.Rates)
            {
                var rate = InterpolateRate(currency1.Rates, tick.Date);
                if (rate != decimal.MinusOne)
                    comboRates.Add(new RateTick(tick.Date, tick.Rate / rate));
            }
            return comboRates;
        }

        private async Task<Currency> Get(string symbol)
        {
            if (!CurrencyList.TryGetValue(symbol, out var currency))
                throw new ArgumentException($"Unknown currency: {symbol}.");
            if (!currency.Rates.Any())
                currency.Rates = await BoeCurrency.Retrieve(symbol, Start, End).ConfigureAwait(false);
            return currency;
        }

        private decimal InterpolateRate(List<RateTick> list, LocalDate date, int tryIndex = -1)
        {
            var len = list.Count;
            if (tryIndex != -1 && date == list[tryIndex].Date)
                return list[tryIndex].Rate;
            if (date < list[0].Date) // not enough data
                return decimal.MinusOne;
            var days = (date - list[len - 1].Date).Days; // future date so use the latest data
            if (days >= 0)
            {
                if (days < 4)
                    return list[len - 1].Rate;
                return decimal.MinusOne;
            }
            var p = list.BinarySearch(new RateTick(date, decimal.MinusOne));
            if (p >= 0) // found
                return list[p].Rate;
            p = ~p; // not found, ~p is next highest position in list; linear interpolation
            var rate = list[p].Rate + ((list[p - 1].Rate - list[p].Rate) / (list[p - 1].Date - list[p].Date).Ticks * (date - list[p].Date).Ticks);
            Logger.LogInformation($"InterpolateRate: {date} => {p} => {rate}.");
            return rate;
        }
    }
}

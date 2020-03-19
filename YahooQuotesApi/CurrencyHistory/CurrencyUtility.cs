using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YahooQuotesApi
{
    public static class CurrencyUtility
    {
        private static readonly string[] NumeratorCurrencySymbols = 
            { "CAD", "CHF", "CZK", "DKK", "HKD", "HUF", "ILS", "JPY", "MXN", "NOK", "PLN", "SEK", "SGD" };
        public static bool IsNumeratorSymbol(string symbol) =>
            NumeratorCurrencySymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YahooQuotesApi
{
    public static class CurrencyUtility
    {
        /*
        public static bool IsCurrencySymbolFormat(string symbol) =>
            !string.IsNullOrEmpty(symbol) &&
            symbol.Length == 8 &&
            symbol.EndsWith("=X") &&
            symbol.Substring(0, 6).All(x => char.IsLetter(x) && char.IsUpper(x));

        public static bool IsCurrencySymbol(string symbol)
        {
            if (symbol == null)
                throw new ArgumentNullException(symbol);
            if (symbol.Length != 3)
                throw new ArgumentException(symbol);
            return BoeCurrencyHistory.Symbols.ContainsKey(symbol);
        }
        */

        private static readonly string[] NumeratorCurrencySymbols = 
            { "CAD", "CHF", "CZK", "DKK", "HKD", "HUF", "ILS", "JPY", "MXN", "NOK", "PLN", "SEK", "SGD" };
        public static bool IsNumeratorSymbol(string symbol) =>
            NumeratorCurrencySymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase);
    }
}

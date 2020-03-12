using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YahooQuotesApi
{
    public static class CurrencyUtility
    {
        public static bool IsCurrencySymbolFormat(string symbol) =>
            !string.IsNullOrEmpty(symbol) &&
            symbol.Length == 8 &&
            symbol.EndsWith("=X") &&
            symbol.Substring(0, 6).All(x => char.IsLetter(x) && char.IsUpper(x));

        public static bool IsCurrencySymbol(string symbol) =>
            IsCurrencySymbolFormat(symbol) &&
            BoeCurrencyHistory.Symbols.ContainsKey(symbol.Substring(0, 3)) &&
            BoeCurrencyHistory.Symbols.ContainsKey(symbol.Substring(3, 3));

        private static readonly string[] NumeratorCurrencySymbols = 
            { "CAD", "CHF", "CZK", "DKK", "HKD", "HUF", "ILS", "JPY", "MXN", "NOK", "PLN", "SEK", "SGD" };
        public static bool IsNumeratorSymbol(string symbol) =>
            NumeratorCurrencySymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase);
    }
}

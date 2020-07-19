using System;
using System.Linq;
using NodaTime;

//https://help.yahoo.com/kb/exchanges-data-providers-yahoo-finance-sln2310.html
//https://support.office.com/en-us/article/about-our-data-sources-98a03e23-37f6-4776-beea-c5a6c8e787e6

namespace YahooQuotesApi
{
    internal static class Exchanges
    {
        internal static LocalTime GetCloseTimeFromSymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                throw new ArgumentException("symbol");

            var suffix = GetSuffix(symbol);

            return suffix switch
            {
                "TO" => new LocalTime(16,  0), // Toronto Stock Exchange (TSX)
                "V"  => new LocalTime(16,  0), // TSXV (Venture)
                "CN" => new LocalTime(16,  0), // Canadian Securities Exchange
                "NE" => new LocalTime(16,  0), // NEO Exchange (Canada)

                "MX" => new LocalTime(15,  0), // Mexico
                "SA" => new LocalTime(18,  0), // Sao Paolo
                "BA" => new LocalTime(17,  0), // Buenos Aires
                "SN" => new LocalTime(17,  0), // Santiago
                "CR" => new LocalTime(17,  0), // Caracas?

                "L"  => new LocalTime(16, 30), // London
                "IL" => new LocalTime(16, 30), // London (IOB)
                "IR" => new LocalTime(16, 30), // Ireland
                "AS" => new LocalTime(17, 30), // Amsterdam
                "BR" => new LocalTime(17, 30), // Brussels
                "PA" => new LocalTime(17, 30), // Paris
                "NX" => new LocalTime(17, 30), // Euronext France
                "MI" => new LocalTime(17, 30), // Milan
                "TI" => new LocalTime(17, 30), // Italy, EuroTLX
                "MA" => new LocalTime(17, 30), // Madrid
                "MC" => new LocalTime(17, 30), // Spain, ICE
                "LS" => new LocalTime(17, 30), // Lisbon
                "DE" => new LocalTime(17, 30), // Deutsche Boerse XETRA
                "F"  => new LocalTime(17, 30), // Frankfurt
                "BE" => new LocalTime(17, 30), // Berlin
                "BM" => new LocalTime(17, 30), // Bremen
                "SG" => new LocalTime(17, 30), // Stuttgart
                "HM" => new LocalTime(17, 30), // Hamburg
                "HA" => new LocalTime(17, 30), // Hanover
                "SW" => new LocalTime(17, 30), // Swiss SIX
                "DU" => new LocalTime(17, 30), // Dusseldorf
                "PR" => new LocalTime(17, 30), // Prague
                "MU" => new LocalTime(17, 30), // Munich
                "VI" => new LocalTime(17, 30), // Vienna
                "BD" => new LocalTime(17, 30), // Budapest
                "AT" => new LocalTime(17, 30), // Athens?
                "ST" => new LocalTime(17, 30), // Nasdaq OMX Stockholm
                "CO" => new LocalTime(17, 30), // Copenhagen
                "OS" => new LocalTime(16, 20), // Oslo
                "OL" => new LocalTime(16, 20), // Oslo
                "HE" => new LocalTime(18, 30), // Helsinki
                "TL" => new LocalTime(16,  0), // Tallinn
                "RG" => new LocalTime(16,  0), // Riga
                "VS" => new LocalTime(16,  0), // Vilnius
                "IC" => new LocalTime(15, 30), // Iceland

                "ME" => new LocalTime(17,  0), // Moscow?
                "SAU"=> new LocalTime(17,  0), // Saudi?
                "QA" => new LocalTime(17,  0), // Qatar?
                "IS" => new LocalTime(17,  0), // Istanbul?
                "CA" => new LocalTime(15,  0), // Egypt?
                "JO" => new LocalTime(17,  0), // Johannesburg

                "TA" => new LocalTime(17, 14), // Tel Aviv
                "BO" => new LocalTime(15, 30), // Bombay
                "NS" => new LocalTime(15, 30), // National Stock Exchange of India

                "T"  => new LocalTime(15,  0), // Tokyo
                "KS" => new LocalTime(15, 30), // Korea
                "KQ" => new LocalTime(15, 30), // Korea?
                "HK" => new LocalTime(16,  0), // Hong Kong
                "SS" => new LocalTime(15,  0), // Shanghai
                "SZ" => new LocalTime(15,  0), // Shenzhen?

                "TW" => new LocalTime(13, 30), // Taiwan
                "TWO"=> new LocalTime(13, 30), // Taiwan OTC
                "BK" => new LocalTime(13, 30), // Thailand?
                "SI" => new LocalTime(17,  0), // Singapore
                "KL" => new LocalTime(17,  0), // Kuala Lumpur
                "JK" => new LocalTime(16,  0), // Jakarta

                "AX" => new LocalTime(16,  0), // Australia
                "NZ" => new LocalTime(16, 45), // New Zealand

                _    => new LocalTime(16,  0)  // US, default  
            };

        }

        private static string GetSuffix(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException($"Invalid symbol: {symbol}.");
            var partsArray = symbol.Split('.');
            var parts = partsArray.Count();
            if (parts == 1)
                return "";
            if (parts == 2)
            {
                var sym = partsArray[0];
                var suffix = partsArray[1];
                if (suffix.Length > 0 && sym.Length > 0)
                    return suffix;
            }
            throw new ArgumentException($"Invalid symbol suffix: {symbol}.");
        }
    }
}

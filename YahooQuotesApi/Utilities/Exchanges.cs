using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YahooQuotesApi
{
    public static class Exchanges
    {
        public static LocalTime GetCloseTimeFromSymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                throw new ArgumentException("symbol");

            var suffix = GetSuffix(symbol);

            return suffix switch
            {
                "MX" => new LocalTime(15,  0), // Mexico
                "SA" => new LocalTime(18,  0), // Sao Paolo
                "BA" => new LocalTime(17,  0), // Buenos Aires
                "SN" => new LocalTime(17,  0), // Santiago

                "L"  => new LocalTime(16, 30), // London
                "AS" => new LocalTime(17, 30), // Amsterdam
                "BR" => new LocalTime(17, 30), // Brussels
                "PA" => new LocalTime(17, 30), // Paris
                "MI" => new LocalTime(17, 30), // Milan
                "MA" => new LocalTime(17, 30), // Madrid
                "LS" => new LocalTime(17, 30), // Lisbon
                "DE" => new LocalTime(17, 30), // Germany
                "F"  => new LocalTime(17, 30), // Frankfurt
                "BE" => new LocalTime(17, 30), // Berlin
                "SG" => new LocalTime(17, 30), // Stuttgart
                "HM" => new LocalTime(17, 30), // Hamburg
                "HA" => new LocalTime(17, 30), // Hanover
                "SW" => new LocalTime(17, 30), // Swiss
                "DU" => new LocalTime(17, 30), // Dusseldorf
                "MU" => new LocalTime(17, 30), // Munich
                "VI" => new LocalTime(17, 30), // Vienna
                "ST" => new LocalTime(17, 30), // Stockholm
                "CO" => new LocalTime(17, 30), // Copenhagen
                "OS" => new LocalTime(16, 20), // Oslo
                "OL" => new LocalTime(16, 20), // Oslo
                "HE" => new LocalTime(18, 30), // Helsinki
                "TL" => new LocalTime(16,  0), // Tallinn
                "RG" => new LocalTime(16,  0), // Riga
                "VS" => new LocalTime(16,  0), // Vilnius
                "IC" => new LocalTime(15, 30), // Iceland

                "TI" => new LocalTime(17, 30), // TLO?
                "IL" => new LocalTime(17, 30), // IOB?
                "JO" => new LocalTime(17,  0), // Johannesburg

                "TA" => new LocalTime(17, 30), // Tel Aviv
                "BO" => new LocalTime(15, 30), // Bombay

                "T"  => new LocalTime(15,  0), // Tokyo
                "KS" => new LocalTime(15, 30), // Korea
                "HK" => new LocalTime(16,  0), // Hong Kong
                "SS" => new LocalTime(15,  0), // Shanghai
                "TW" => new LocalTime(13, 30), // Taiwan
                "TWO"=> new LocalTime(13, 30), // Taiwan
                "SI" => new LocalTime(17,  0), // Singapore
                "KL" => new LocalTime(17,  0), // Kuala Lumpur
                "JK" => new LocalTime(16,  0), // Jakarta

                "AX" => new LocalTime(16,  0), // Australia
                "NZ" => new LocalTime(16, 45), // New Zealand

                _    => new LocalTime(16,  0)  // US, Canada, Cairo, default  
            };

        }

        internal static string GetSuffix(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException($"Invalid symbol suffix: {symbol}.");
            var partsArray = symbol.Split('.');
            var parts = partsArray.Count();
            if (parts == 1)
                return "";
            if (parts == 2)
            {
                var symb = partsArray[0];
                var suffix = partsArray[1];
                if (suffix.Length > 0 && symb.Length > 0)
                    return suffix;
            }
            throw new ArgumentException($"Invalid symbol suffix: {symbol}.");
        }
    }
}

using NodaTime;

//https://help.yahoo.com/kb/exchanges-data-providers-yahoo-finance-sln2310.html
//https://support.office.com/en-us/article/about-our-data-sources-98a03e23-37f6-4776-beea-c5a6c8e787e6

namespace YahooQuotesApi
{
    internal static class Exchanges
    {
        internal static LocalTime GetCloseTimeFromSymbol(Symbol symbol)
        {
            if (symbol.IsCurrencyRate)
                return new LocalTime(16, 0, 0);

            var suffix = symbol.Suffix;

            (int hour, int minute) = suffix switch
            {
                "TO" => (16,  0), // Toronto Stock Exchange (TSX)
                "V"  => (16,  0), // TSXV (Venture)
                "CN" => (16,  0), // Canadian Securities Exchange
                "NE" => (16,  0), // NEO Exchange (Canada)

                "MX" => (15,  0), // Mexico
                "SA" => (18,  0), // Sao Paolo
                "BA" => (17,  0), // Buenos Aires
                "SN" => (17,  0), // Santiago
                "CR" => (17,  0), // Caracas?

                "L"  => (16, 30), // London
                "IL" => (16, 30), // London (IOB)
                "IR" => (16, 30), // Ireland
                "AS" => (17, 30), // Amsterdam
                "BR" => (17, 30), // Brussels
                "PA" => (17, 30), // Paris
                "NX" => (17, 30), // Euronext France
                "MI" => (17, 30), // Milan
                "TI" => (17, 30), // Italy, EuroTLX
                "MA" => (17, 30), // Madrid
                "MC" => (17, 30), // Spain, ICE
                "LS" => (17, 30), // Lisbon
                "DE" => (17, 30), // Deutsche Boerse XETRA
                "F"  => (17, 30), // Frankfurt
                "BE" => (17, 30), // Berlin
                "BM" => (17, 30), // Bremen
                "SG" => (17, 30), // Stuttgart
                "HM" => (17, 30), // Hamburg
                "HA" => (17, 30), // Hanover
                "SW" => (17, 30), // Swiss SIX
                "DU" => (17, 30), // Dusseldorf
                "PR" => (17, 30), // Prague
                "MU" => (17, 30), // Munich
                "VI" => (17, 30), // Vienna
                "BD" => (17, 30), // Budapest
                "AT" => (17, 30), // Athens?
                "ST" => (17, 30), // Nasdaq OMX Stockholm
                "CO" => (17, 30), // Copenhagen
                "OS" => (16, 20), // Oslo
                "OL" => (16, 20), // Oslo
                "HE" => (18, 30), // Helsinki
                "TL" => (16,  0), // Tallinn
                "RG" => (16,  0), // Riga
                "VS" => (16,  0), // Vilnius
                "IC" => (15, 30), // Iceland

                "ME" => (17,  0), // Moscow?
                "SAU"=> (17,  0), // Saudi?
                "QA" => (17,  0), // Qatar?
                "IS" => (17,  0), // Istanbul?
                "CA" => (15,  0), // Egypt?
                "JO" => (17,  0), // Johannesburg

                "TA" => (17, 14), // Tel Aviv
                "BO" => (15, 30), // Bombay
                "NS" => (15, 30), // National Stock Exchange of India

                "T"  => (15,  0), // Tokyo
                "KS" => (15, 30), // Korea
                "KQ" => (15, 30), // Korea?
                "HK" => (16,  0), // Hong Kong
                "SS" => (15,  0), // Shanghai
                "SZ" => (15,  0), // Shenzhen?

                "TW" => (13, 30), // Taiwan
                "TWO"=> (13, 30), // Taiwan OTC
                "BK" => (13, 30), // Thailand?
                "SI" => (17,  0), // Singapore
                "KL" => (17,  0), // Kuala Lumpur
                "JK" => (16,  0), // Jakarta

                "AX" => (16,  0), // Australia
                "NZ" => (16, 45), // New Zealand

                _    => (16,  0)  // US, default  
            };

            return new LocalTime(hour, minute);
        }
    }
}

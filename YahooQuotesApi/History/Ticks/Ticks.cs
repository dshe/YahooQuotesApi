// record: get/init, equality, ToString(), deconstruct

/* AAPL: Nasdaq Official Closing Price (NOCP)
02/19/2021 $129.87 (official) => 129.869995 (text from Yahoo Finance)
It seems like the text values were floats converted to double.
So to get the original value:

float f = float.Parse("129.869995");
decimal dec = new decimal(f);
double d = Convert.ToDouble(dec);
(this adjustment is not performed)

Yahoo Finance prices are adjusted for splits.
Yahoo Finance "Adjusted" price is adjusted for dividends.
*/

namespace YahooQuotesApi;

interface ITick
{
    public LocalDate Date { get; }
}

public sealed record class PriceTick(LocalDate Date, double Open, double High, double Low, double Close, double AdjustedClose, long Volume) : ITick;
public sealed record class DividendTick(LocalDate Date, double Dividend) : ITick;
public sealed record class SplitTick(LocalDate Date, double BeforeSplit, double AfterSplit) : ITick;

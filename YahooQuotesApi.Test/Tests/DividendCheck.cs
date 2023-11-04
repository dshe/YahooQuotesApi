using NodaTime;
using NodaTime.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests;

public class Rec
{
    public LocalDate Date { get; init; }
    public double Close { get; init; }
    public double AdjustedClose { get; init; }
    public double Dividend { get; set; }
    public double Adjustment { get; set; }
    public double AdjustmentUsingDividend { get; set; }
    public double BeforeSplit { get; set; }
    public double AfterSplit { get; set; }
    public long Volume { get; init; }
    public override string ToString() => $"{LocalDatePattern.Iso.Format(Date)}, {Close}, {AdjustedClose}, {Dividend}, {Adjustment}, {AdjustmentUsingDividend}";
}

public class DividendCheck : XunitTestBase
{
    private readonly YahooQuotes YahooQuotes;

    public DividendCheck(ITestOutputHelper output) : base(output) =>
        YahooQuotes = new YahooQuotesBuilder().WithLogger(Logger).Build();

    [Fact]
    public async Task Test()
    {
        //await CheckDividends("IBM");
        //await CheckDividends("MSFT");
        //await CheckDividends("EIMI.L"); // accumulatiung
        await CheckDividends("TUR"); // distributing
        //await CheckDividends("TUR.PA"); // accumulatiung
        //await CheckDividends("ITKY.AS"); // distributing but dividends missing!

    }

    public async Task<List<Rec>> CheckDividends(string symbol)
    {
        var security = await YahooQuotes.GetAsync(symbol, Histories.All);
        var prices = security!.PriceHistory.Value;
        var dividends = security!.DividendHistory.Value; // may be incomplete

        List<Rec> recs = new();

        for (var i = prices.Length - 1; i >= 0; i--)
        {
            var price = prices[i];
            recs.Add(new Rec()
            {
                Date = price.Date,
                Close = price.Close,
                AdjustedClose = price.AdjustedClose,
                Volume = price.Volume
            });
        }

        Write($"Symbol {symbol} dividends: {dividends.Length}.");
        foreach (var dividend in dividends)
        {
            Rec? rec = recs.Where(rec => rec.Date == dividend.Date).SingleOrDefault();
            if (rec is null)
                Write($"Symbol {symbol}: Could not find dividend!");
            else
                rec.Dividend = dividend.Dividend;
        }

        recs[0].Adjustment = 1;
        for (var i = 1; i < recs.Count; i++)
        {
            recs[i].Adjustment = (1 - recs[i - 1].Dividend / recs[i].Close) * recs[i - 1].Adjustment;
            recs[i].AdjustmentUsingDividend = recs[i].Close * recs[i].Adjustment;
            double diff = Math.Abs(recs[i].AdjustmentUsingDividend - recs[i].AdjustedClose);
            if (diff > .001)
                Write($"Symbol {symbol}: Invalid adjustment");
        }

        return recs;
    }
}


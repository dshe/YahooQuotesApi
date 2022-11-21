using Microsoft.Extensions.Logging;
using NodaTime.Text;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YahooQuotesApi;

internal static class TickParser
{
    private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

    internal static async Task<ITick[]> ToTicks<T>(this StreamReader streamReader, ILogger logger) where T : ITick
    {
        // Sometimes currencies end with two rows having the same date.
        // Sometimes ticks are returned in seemingly random order.
        // So use a dictionary to clean data.
        Dictionary<LocalDate, ITick> ticks = new(0x100);

        // read header
        await streamReader.ReadLineAsync().ConfigureAwait(false);

        // add ticks to dictionary
        while (!streamReader.EndOfStream)
        {
            string? row = await streamReader.ReadLineAsync().ConfigureAwait(false);
            if (row is null)
                continue;
            ITick? tick = GetTick<T>(row);
            if (tick is null)
                continue;
            if (ticks.TryGetValue(tick.Date, out ITick? tick1))
                logger.LogInformation("Ticks have same date: {Tick1} => {Tick}", tick1, tick);
            ticks[tick.Date] = tick; // Add or update (keep the latest).
        }
        return ticks.Values.OrderBy(x => x.Date).ToArray();
    }

    private static ITick? GetTick<T>(string row) where T : ITick
    {
        // If you don't care about allocations, well, then you can just use String.Split.
        string[] column = row.Split(',');

        LocalDate date = column[0].ToDate();
        if (typeof(T) == typeof(PriceTick))
        {
            if (column[5] == "null")
                return null;
            return new PriceTick(date, column[1].ToDouble(), column[2].ToDouble(), column[3].ToDouble(), column[4].ToDouble(), column[5].ToDouble(), column[6].ToLong());
        }
        if (typeof(T) == typeof(DividendTick))
            return new DividendTick(date, column[1].ToDouble());
        if (typeof(T) == typeof(SplitTick))
        {
            string[] split = column[1].Split(new[] { ':', '/' });
            if (split.Length != 2)
                throw new InvalidOperationException("Split separator not found.");
            return new SplitTick(date, split[1].ToDouble(), split[0].ToDouble());
        }
        throw new InvalidOperationException("Tick type.");
    }

    internal static LocalDate ToDate(this string str)
    {
        ParseResult<LocalDate> result = DatePattern.Parse(str);
        if (result.Success)
            return result.Value;

        throw new InvalidDataException($"Could not convert '{str}' to LocalDate.", result.Exception);
    }

    internal static double ToDouble(this string str)
    {
        if (str == "null")
            return 0d;

        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
            return result.RoundToSigFigs(7);

        throw new InvalidDataException($"Could not convert '{str}' to Double.");
    }

    internal static long ToLong(this string str)
    {
        if (str == "null")
            return 0L;

        if (long.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out long result))
            return result;

        throw new InvalidDataException($"Could not convert '{str}' to Long.");
    }
}

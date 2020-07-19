using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;

namespace YahooQuotesApi
{
    internal static class FieldModifier
    {
        internal static void Modify(string symbol, IDictionary<string, dynamic> d)
        {
            //var before = GetData(d);
            d.Add("ExchangeCloseTime", Exchanges.GetCloseTimeFromSymbol(symbol));
            ChangeFieldName(d, "RegularMarketTime", "RegularMarketTimeSeconds");
            ChangeFieldName(d, "PreMarketTime", "PreMarketTimeSeconds");
            ChangeFieldName(d, "PostMarketTime", "PostMarketTimeSeconds");
            if (d.TryGetValue("ExchangeTimezoneName", out var timezoneName))
            {
                var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timezoneName) ?? throw new TimeZoneNotFoundException(timezoneName);
                d.Add("ExchangeTimezone", tz);
                AddField(d, "RegularMarketTimeSeconds", "RegularMarketTime", s => Instant.FromUnixTimeSeconds(s).InZone(tz));
                AddField(d, "PreMarketTimeSeconds", "PreMarketTime", s => Instant.FromUnixTimeSeconds(s).InZone(tz));
                AddField(d, "PostMarketTimeSeconds", "PostMarketTime", s => Instant.FromUnixTimeSeconds(s).InZone(tz));
            }
            ChangeThenAddField(d, "DividendDate", "DividendDateSeconds", s => Instant.FromUnixTimeSeconds(s).InUtc().LocalDateTime);
            AddField(d, "EarningsTimestamp", "EarningsTime", s => Instant.FromUnixTimeSeconds(s).InUtc().LocalDateTime);
            AddField(d, "EarningsTimestampEnd", "EarningsTimeEnd", s => Instant.FromUnixTimeSeconds(s).InUtc().LocalDateTime);
            AddField(d, "EarningsTimestampStart", "EarningsTimeStart", s => Instant.FromUnixTimeSeconds(s).InUtc().LocalDateTime);
            AddField(d, "FirstTradeDateMilliseconds", "FirstTradeDate", ms => Instant.FromUnixTimeMilliseconds(ms).InUtc().LocalDateTime);
            //var after = GetData(d);
            //var intersection = before.XOr(after);
        }

        private static List<string> GetData(IDictionary<string, dynamic> d) =>
            d.Select(x => $"{x.Key} = {x.Value.ToString()}").OrderBy(x => x).ToList();

        private static void ChangeFieldName(IDictionary<string, dynamic> dictionary, string oldFieldName, string newFieldName)
        {
            if (dictionary.TryGetValue(oldFieldName, out var value) && dictionary.Remove(oldFieldName))
                dictionary.Add(newFieldName, value);
        }

        private static void AddField(IDictionary<string, dynamic> dictionary, string fromFieldName, string newFieldName, Func<dynamic, dynamic> func)
        {
            if (dictionary.TryGetValue(fromFieldName, out var value))
                dictionary.Add(newFieldName, func(value));
        }

        private static void ChangeThenAddField(IDictionary<string, dynamic> dictionary, string oldFieldName, string newFieldName, Func<dynamic, dynamic> func)
        {
            if (dictionary.TryGetValue(oldFieldName, out var value))
            {
                dictionary.Add(newFieldName, value);
                dictionary[oldFieldName] = func(value);
            }
        }

    }
}

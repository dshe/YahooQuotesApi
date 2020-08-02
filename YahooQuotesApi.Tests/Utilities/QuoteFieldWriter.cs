using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests
{
    public class QuoteFieldWriter
    {
        private readonly Action<string> Write;
        public QuoteFieldWriter(ITestOutputHelper output) => Write = output.WriteLine;

        private async Task<List<KeyValuePair<string, object>>> GetFields()
        {
            var syms = new[] { "C", "2800.HK", "JPYUSD=X", "HXT.TO", "NAB.AX" }; // Sydney +10 };
            var symbols = syms.Select(x => new Symbol(x)).ToList();

            var results = await new Snapshot(NullLogger.Instance, Duration.Zero).GetAsync(symbols, default);
            var dict = results.Values.SelectMany(x => x).DistinctBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

            // available only before the market opens
            dict["PreMarketPrice"] = 1m; // decimal
            dict["PreMarketTime"] = new ZonedDateTime();
            dict["PreMarketTimeSeconds"] = 1L; // int64
            dict["PreMarketChange"] = 1m; // decimal
            dict["PreMarketChangePercent"] = 1m; // decimal

            // available only after the market closes
            dict["PostMarketPrice"] = 1m; // decimal
            dict["PostMarketTime"] = new ZonedDateTime();
            dict["PostMarketTimeSeconds"] = 1L; // int64
            dict["PostMarketChange"] = 1m; // decimal
            dict["PostMarketChangePercent"] = 1m; // decimal

            dict["PriceHistory"] = ""; // (IReadOnlyList<PriceTick>) new List<PriceTick>();
            dict["PriceHistoryBase"] = ""; // (IReadOnlyList<PriceTick>) new List<PriceTick>();
            dict["DividendHistory"] = "";
            dict["SplitHistory"] = "";

            return dict.OrderBy(x => x.Key).ToList();
            ;
        }


        [Fact]
        public async Task MakePropertiesList()
        {
            var fields = await GetFields();
            Write($"// Fields.cs: {fields.Count}. This list was generated automatically from names been defined by Yahoo, mostly.");
            Write(string.Join(", ", fields.Select(x => x.Key)));
            Write(".");
            Write(Environment.NewLine);
        }

        [Fact]
        public async Task MakePropertyList()
        {
            var fields = await GetFields();

            Write($"// Security.cs: {fields.Count}. This list was generated automatically from names defined by Yahoo, mostly.");
            foreach (var field in fields)
            {
                var name = field.Key;
                var value = field.Value;
                Type type = value.GetType();

                if (name == "PriceHistory")
                {
                    //var historyType = name.Substring(0, name.IndexOf("History"));
                    Write($"public IReadOnlyList<PriceTick>? {name} => GetN();");
                    continue;
                }
                if (name == "PriceHistoryBase")
                {
                    //var historyType = name.Substring(0, name.IndexOf("History"));
                    Write($"public IReadOnlyList<PriceTick>? {name} => GetN();");
                    continue;
                }
                var typeName = type.Name;
                if (typeName == "CachedDateTimeZone") // may be a NodaTime bug
                    typeName = "DateTimeZone";
                if (typeName == "String") // Symbol, Currency
                    Write($"public {typeName} {name} => GetS();");
                else
                    Write($"public {typeName}? {name} => GetN();");
            }
            Write(Environment.NewLine);
        }

        [Fact]
        public async Task CompareEnums()
        {
            var propertyNames = typeof(Security).GetProperties().Select(p => p.Name).ToList();
            var newList = (await GetFields()).Select(f => f.Key).ToList();

            var combinedList = propertyNames.ToList(); // make copy
            combinedList.AddRange(newList);
            combinedList = combinedList.Distinct().OrderBy(s => s).ToList();

            Write("Updates:");

            foreach (var item in combinedList)
            {
                if (propertyNames.Find(x => x == item) == null)
                    Write($"new: {item}");
                if (newList.Find(x => x == item) == null && item != "Fields" && item != "Item")
                    Write($"removed: {item}");
            }
        }
    }
}

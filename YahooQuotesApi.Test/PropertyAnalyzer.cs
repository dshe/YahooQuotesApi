using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooQuotesApi.Tests;

public class PropertyAnalyzer : XunitTestBase
{
    public PropertyAnalyzer(ITestOutputHelper output) : base(output) { }

    private static readonly Lazy<Task<List<Prop>>> Props = new(async () =>
    {
        var symbols = new[] { "MSFT", "SPY", "2800.HK", "HXT.TO", "NAB.AX", "JPYUSD=X", "ES=F" };
        Dictionary<string, Security?> results = await new YahooQuotesBuilder().Build().GetAsync(symbols);
        List<Prop> props = results.Values.NotNull().Select(s => s.Props.Values).SelectMany(x => x).ToList();

        //Missing, New(ok), Expected, Calculated(ok)
        //remove missing properties if any expected property exists
        List<Prop> expected = props.Where(p => p.Category == PropCategory.Expected).ToList();
        IEnumerable<Prop> removals = props
            .Where(p => p.Category == PropCategory.Missing)
            .Where(p => expected.Exists(x => x.Name == p.Name));
        foreach (Prop p in removals.ToList())
            props.Remove(p);

        props = props.DistinctBy(x => x.Name)
            .Where(x => x.Name != "Props")
            .OrderBy(n => n.Name)
            .ToList();

        return props;
    });

    private async Task ListProperties(Func<IEnumerable<Prop>, IEnumerable<Prop>> process)
    {
        List<Prop> props = await Props.Value;
        props = process(props).ToList();

        StringBuilder sb = new();

        foreach (Prop p in props)
        {
            sb.AppendFormat("{0,-12}", p.Category);
            sb.AppendFormat("{0,-35}", p.Name);

            if (p.JProperty == null)
                sb.AppendFormat("{0,-60}", p.Value);
            else
            {
                sb.AppendFormat("{0,-25}", p.Value);
                sb.AppendFormat("{0,-25}", p.JProperty.Value.Value.GetRawText());
                sb.AppendFormat("{0,-10}", p.JProperty.Value.Value.ValueKind);
            }
            if (p.PropertyInfo != null)
                sb.Append(p.PropertyInfo.PropertyType);
            sb.AppendLine();
        }
        Write(sb.ToString());
    }

    [Fact]
    public async Task ListPropertiesByOrderDeclared() => await ListProperties(x => x.Where(x => x.PropertyInfo != null).OrderBy(x => x.PropertyInfo!.MetadataToken));

    [Fact]
    public async Task ListPropertiesByCategory() => await ListProperties(x => x.OrderBy(x => x.Category).ThenBy(x => x.Name));

    [Fact]
    public async Task ListPropertiesByName() => await ListProperties(x => x.OrderBy(x => x.Name));

    [Fact]
    public async Task ListPropertiesByType() => await ListProperties(x => x.Where(x => x.PropertyInfo != null).OrderBy(x => x.PropertyInfo!.PropertyType.ToString()).ThenBy(x => x.Name));
}

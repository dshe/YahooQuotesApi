namespace YahooQuotesApi.UtilityTests;

public class SymbolTest(ITestOutputHelper output) : XunitTestBase(output)
{
    [Fact]
    public void TestArgs()
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        Assert.Throws<ArgumentNullException>(() => Symbol.Create(((string?)null)));
#pragma warning restore CS8625

        Assert.Throws<ArgumentException>(() => Symbol.Create("with space"));

        Assert.Throws<ArgumentException>(() => Symbol.Create(""));

        Symbol stock = Symbol.Create("ABC");
        Assert.True(stock.IsValid && stock.IsSecurity && !stock.IsCurrency && !stock.IsCurrencyRate);

        Symbol currency = Symbol.Create("ABC=X");
        Assert.True(currency.IsValid && !currency.IsSecurity && currency.IsCurrency && !currency.IsCurrencyRate);
        Assert.Equal("ABC", currency.Currency);

        Symbol rate = Symbol.Create("ABCDEF=X");
        Assert.True(rate.IsValid && !rate.IsSecurity && !rate.IsCurrency && rate.IsCurrencyRate);
        Assert.Equal("DEF", rate.QuoteCurrency);

        var _ = Symbol.Create("GC=F"); // furues

        Assert.Throws<ArgumentException>(() => Symbol.Create("ABCABC=X"));
        Assert.Throws<ArgumentException>(() => Symbol.Create("ABCBC=X"));
        Assert.Throws<ArgumentException>(() => Symbol.Create("=XABC"));
    }

    [Fact]
    public void TestOperators()
    {
        Assert.True(Symbol.Create("abc") == Symbol.Create("abc"));
        Assert.Equal(Symbol.Create("abc"), Symbol.Create("abc"));
        Assert.NotEqual(Symbol.Create("abc"), Symbol.Create("def"));

        var list = new List<Symbol>
            {
                Symbol.Create("c5"),
                Symbol.Create("c2"),
                Symbol.Create("c1"),
                Symbol.Create("c4")
            };

        list.Sort();
        Assert.Equal(Symbol.Create("c1"), list.First());

        var dict = list.ToDictionary(x => x, x => x.Name);
        var result = dict[Symbol.Create("c2")];
        Assert.Equal("C2", result);
    }

    [Fact]
    public void TestDefaultEquality()
    {
        Symbol defaultSymbol;
#pragma warning disable CS8887 // Use of unassigned local variable
        Assert.False(defaultSymbol.IsValid); // IsValid does not throw
#pragma warning restore CS8887 // Use of unassigned local variable

        Assert.True(defaultSymbol == default);
        Assert.False(defaultSymbol.IsValid);

        Symbol.TryCreate("", out Symbol sss);
        Assert.False(sss.IsValid);
        Assert.Equal(default, sss);
    }

    [Theory]
    [InlineData("A", "")]
    [InlineData(".", "")]
    [InlineData("A.", "")]
    [InlineData(".A", "A")]
    [InlineData("A.B", "B")]
    [InlineData("ABC.DEF", "DEF")]
    public void TestSuffix(string symbolName, string suffix)
    {
        var symbol = Symbol.Create(symbolName);
        Assert.Equal(suffix, symbol.Suffix);
    }
}

namespace YahooQuotesApi.UtilityTests;

public class CookieAndCrumbTests : XunitTestBase
{
    private readonly YahooQuotes YahooQuotes;
    public CookieAndCrumbTests(ITestOutputHelper output) : base(output, LogLevel.Trace) =>
        YahooQuotes = new YahooQuotesBuilder().WithLogger(Logger).Build();

    [Fact]
    public async Task TestCookieAndCrumb()
    {
        (string[] cookies, string crumb) = await YahooQuotes.GetCookieAndCrumbAsync();
        Assert.NotEmpty(crumb);
        Assert.NotEmpty(cookies);
    }

}


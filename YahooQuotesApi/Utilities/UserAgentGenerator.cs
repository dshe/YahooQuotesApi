namespace YahooQuotesApi;

internal static class UserAgentGenerator
{
    private static readonly string[] UserAgents =
    [
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_12_5) AppleWebKit/603.2.4 (KHTML, like Gecko) Version/10.1.1 Safari/603.2.4",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_13_6) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/11.1 Safari/605.1",
        "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; AS; rv:11.0) like Gecko",
        "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:54.0) Gecko/20100101 Firefox/54.0",
        "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.106 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.79 Safari/537.36 Edge/14.14393",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36 Edg/115.0.1901.183",
        "Mozilla/5.0 (Windows; U; Windows NT 10.2; Win64; x64; en-US) AppleWebKit/534.7 (KHTML, like Gecko) Chrome/49.0.2531.156 Safari/536.8 Edge/14.23513",
        "Mozilla/5.0 (Linux x86_64) AppleWebKit/535.17 (KHTML, like Gecko) Chrome/50.0.2691.383 Safari/536",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_3_6; en-US) AppleWebKit/533.5 (KHTML, like Gecko) Chrome/51.0.2356.308 Safari/600",
        "Mozilla/5.0 (Android; Android 5.0; HTC [M8|M9|M8 Pro Build/LRX22G) AppleWebKit/535.47 (KHTML, like Gecko)  Chrome/50.0.2242.149 Mobile Safari/533.0",
        "Mozilla/5.0 (iPhone; CPU iPhone OS 8_9_9; like Mac OS X) AppleWebKit/603.38 (KHTML, like Gecko)  Chrome/52.0.1130.144 Mobile Safari/601.2",
        "Mozilla/5.0 (iPad; CPU iPad OS 11_9_2 like Mac OS X) AppleWebKit/600.34 (KHTML, like Gecko)  Chrome/49.0.2764.118 Mobile Safari/534.4",
        "Mozilla/5.0 (Linux; Linux x86_64; en-US) AppleWebKit/537.35 (KHTML, like Gecko) Chrome/53.0.2585.246 Safari/601",
        "Mozilla/5.0 (Linux; Linux x86_64; en-US) Gecko/20130401 Firefox/67.8",
        "Mozilla/5.0 (Windows NT 10.2; x64) AppleWebKit/534.45 (KHTML, like Gecko) Chrome/55.0.3502.198 Safari/600.8 Edge/9.69335",
        "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:61.0) Gecko/20100101 Firefox/61.0"
    ];

    private static readonly Random random = new();

#pragma warning disable CA5394

    public static string GetRandom() => UserAgents[random.Next(UserAgents.Length)];
}

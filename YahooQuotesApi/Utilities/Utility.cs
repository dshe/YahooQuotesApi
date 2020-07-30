using System;
using NodaTime;

namespace YahooQuotesApi
{
    internal static class Utility
    {
        internal static IClock Clock { get; set; } = SystemClock.Instance;

        internal static string GetRandomString(int length) =>
            Guid.NewGuid().ToString().Substring(0, length);
    }
}

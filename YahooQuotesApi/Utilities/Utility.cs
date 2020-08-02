using System;
using NodaTime;

namespace YahooQuotesApi
{
    internal static class Utility
    {
        internal static string GetRandomString(int length) =>
            Guid.NewGuid().ToString().Substring(0, length);
    }
}

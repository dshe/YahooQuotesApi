using System;

namespace YahooQuotesApi
{
    internal static class Utility
    {
        internal static string GetRandomString(int length) =>
            Guid.NewGuid().ToString().Substring(0, length);


        internal static double RoundToSigFigs(this double num, int figs)
        {
            if (num == 0)
                return 0;

            double d = Math.Ceiling(Math.Log10(num < 0 ? -num : num));
            int power = figs - (int)d;

            double magnitude = Math.Pow(10, power);
            double shifted = Math.Round(num * magnitude);
            return shifted / magnitude;
        }

    }

}

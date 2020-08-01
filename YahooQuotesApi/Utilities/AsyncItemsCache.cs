using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;

namespace YahooQuotesApi
{
    /*
    * TResult - the type of result to be cached
    * TKey - the type of the key used to identify the result
    */
    internal class AsyncItemsCache<TKey, TResult>
    {
        private readonly IClock Clock;
        private readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1);
        private readonly Dictionary<TKey, (Instant, TResult)> Cache = new Dictionary<TKey, (Instant, TResult)>();
        private readonly Duration Duration;

        internal AsyncItemsCache(Duration cacheDuration) : this(SystemClock.Instance, cacheDuration) { }
        internal AsyncItemsCache(IClock clock, Duration cacheDuration)
        {
            Clock = clock;
            Duration = cacheDuration;
        }

        internal async Task<Dictionary<TKey, TResult>> Get(List<TKey> keys, Func<Task<Dictionary<TKey, TResult>>> factory)
        {
            await Semaphore.WaitAsync().ConfigureAwait(false); // serialize
            try
            {
                var now = Clock.GetCurrentInstant();

                var results = GetFromCache(keys, now);

                if (!results.Any())
                {
                    results = await factory().ConfigureAwait(false);
                    foreach (var kvp in results)
                        Cache[kvp.Key] = (now, kvp.Value);
                }

                return results;
            }
            finally
            {
                Semaphore.Release();
            }
        }

        private Dictionary<TKey, TResult> GetFromCache(List<TKey> keys, Instant now)
        {
           var results = new Dictionary<TKey, TResult>();

            foreach (var key in keys)
            {
                if (Cache.TryGetValue(key, out (Instant time, TResult value) item) && now - item.time <= Duration)
                    results.Add(key, item.value);
                else
                {
                    results.Clear();
                    break;
                }

            }
            return results;
        }

        internal async Task Clear()
        {
            await Semaphore.WaitAsync().ConfigureAwait(false);
            Cache.Clear();
            Semaphore.Release();
        }
    }
}

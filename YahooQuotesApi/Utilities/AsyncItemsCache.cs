using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Dictionary<TKey, (TResult, Instant)> Cache = new Dictionary<TKey, (TResult, Instant)>();
        private readonly Duration Duration;

        internal AsyncItemsCache(Duration cacheDuration) : this(SystemClock.Instance, cacheDuration) { }
        internal AsyncItemsCache(IClock clock, Duration cacheDuration)
        {
            Clock = clock;
            Duration = cacheDuration;
        }

        internal async Task<Dictionary<TKey, TResult>> Get(List<TKey> keys, Func<Task<Dictionary<TKey, TResult>>> factory)
        {
            await Semaphore.WaitAsync().ConfigureAwait(false); // serialize requests
            try
            {
                var now = Clock.GetCurrentInstant();

                var dictionary = GetFromCache(keys, now);

                if (!dictionary.Any())
                {
                    dictionary = await factory().ConfigureAwait(false);
                    foreach (var kvp in dictionary)
                        Cache[kvp.Key] = (kvp.Value, now);
                }

                return dictionary;
            }
            finally
            {
                Semaphore.Release();
            }
        }

        // Return results only if results for all keys are present in the cache and not expired.
        private Dictionary<TKey, TResult> GetFromCache(List<TKey> keys, Instant now)
        {
            var results = new Dictionary<TKey, TResult>(keys.Count);

            foreach (var key in keys)
            {
                if (Cache.TryGetValue(key, out (TResult value, Instant time) item) && now - item.time <= Duration)
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

using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    internal class AsyncItemsCache<TKey, TResult>
    {
        private readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        private readonly AsyncQueue<TKey> Pending;
        private readonly Cache<TKey, TResult> Cache;
        private readonly Func<IEnumerable<TKey>, CancellationToken, Task<Dictionary<TKey, TResult>>> Produce;

        internal AsyncItemsCache(IClock clock, Duration cacheDuration, int delay, Func<IEnumerable<TKey>, CancellationToken, Task<Dictionary<TKey, TResult>>> produce)
        {
            Pending = new AsyncQueue<TKey>(delay);
            Cache = new Cache<TKey, TResult>(clock, cacheDuration);
            Produce = produce;
        }

        internal async Task<Dictionary<TKey, TResult>> Get(HashSet<TKey> keys, CancellationToken ct)
        {
            if (Cache.TryGetAll(keys, out var results))
               return results;

            Pending.Add(keys);

            await Semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (Pending.Count() > 0)
                {
                    var items = await Pending.RemoveAllAsync(ct);
                    var dictionary = await Produce(items, ct).ConfigureAwait(false);
                    Cache.Store(dictionary);
                }
            }
            finally
            {
                Semaphore.Release();
            }

            return Cache.GetAll(keys);
        }
    }
}

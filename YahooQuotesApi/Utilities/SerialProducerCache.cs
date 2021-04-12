using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    internal class SerialProducerCache<TKey, TResult>
    {
        private readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        private readonly List<TKey> Buffer = new List<TKey>();
        private readonly Cache<TKey, TResult> Cache;
        private readonly Func<IEnumerable<TKey>, CancellationToken, Task<Dictionary<TKey, TResult>>> Produce;

        internal SerialProducerCache(IClock clock, Duration cacheDuration, Func<IEnumerable<TKey>, CancellationToken, Task<Dictionary<TKey, TResult>>> produce)
        {
            Cache = new Cache<TKey, TResult>(clock, cacheDuration);
            Produce = produce;
        }

        internal async Task<Dictionary<TKey, TResult>> Get(HashSet<TKey> keys, CancellationToken ct)
        {
            if (Cache.TryGetAll(keys, out var results))
                return results;

            lock (Buffer)
            {
                Buffer.AddRange(keys);
            }

            await Semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                List<TKey> items;
                lock (Buffer)
                {
                    items = new List<TKey>(Buffer);
                    Buffer.Clear();
                }
                if (items.Any())
                {
                    results = await Produce(items, ct).ConfigureAwait(false);
                    Cache.Save(results);
                }
            }
            finally
            {
                Semaphore.Release();
            }

            return Cache.Get(keys);
        }
    }
}

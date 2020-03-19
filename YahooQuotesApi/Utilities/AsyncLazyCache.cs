using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    internal class AsyncLazyCache<T1, T2>
    {
        private readonly Dictionary<T1, Task<T2>> TaskCache = new Dictionary<T1, Task<T2>>();
        private readonly Func<T1, CancellationToken, Task<T2>> Producer;

        internal AsyncLazyCache(Func<T1, CancellationToken, Task<T2>> producer) => Producer = producer;

        internal async Task<T2> Get(T1 key, CancellationToken ct = default)
        {
            Task<T2> task;
            lock (TaskCache)
            {
                if (!TaskCache.TryGetValue(key, out task))
                {
                    task = Producer(key, ct);
                    TaskCache.Add(key, task);
                }
            }
            return await task.ConfigureAwait(false);
        }

        internal void Clear()
        {
            lock (TaskCache)
            {
                TaskCache.Clear();
            }
        }
    }
}

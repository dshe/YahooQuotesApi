using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    public class AsyncLazyCache<T1, T2>
    {
        private readonly Dictionary<T1, Task<T2>> TaskCache = new Dictionary<T1, Task<T2>>();
        private readonly Func<T1, CancellationToken, Task<T2>> Produce;

        public AsyncLazyCache(Func<T1, CancellationToken, Task<T2>> produce) => Produce = produce;

        public async Task<T2> Get(T1 key, CancellationToken ct = default)
        {
            Task<T2> task;
            lock (TaskCache)
            {
                if (!TaskCache.TryGetValue(key, out task))
                {
                    task = Produce(key, ct);
                    TaskCache.Add(key, task);
                }
            }
            return await task.ConfigureAwait(false);
        }
    }
}

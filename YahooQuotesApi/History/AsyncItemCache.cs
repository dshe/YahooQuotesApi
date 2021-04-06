using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;

namespace YahooQuotesApi
{
    /*
     * TResult - the type of result to be cached
     * TKey - the type of the key used to identify the result
     */
    internal class AsyncItemCache<TKey, TResult>
    {
        private readonly IClock Clock;
        private readonly Dictionary<TKey, (Task<TResult>, Instant)> TaskCache = new Dictionary<TKey, (Task<TResult>, Instant)>();
        private readonly Duration Duration;

        internal AsyncItemCache(IClock clock, Duration cacheDuration)
        {
            Duration = cacheDuration;
            Clock = clock;
        }

        internal async Task<TResult> Get(TKey key, Func<Task<TResult>> producer)
        {
            (Task<TResult> task, Instant time) item;
            lock (TaskCache)
            {
                var now = Clock.GetCurrentInstant();
                if (!TaskCache.TryGetValue(key, out item) || now - item.time > Duration)
                {
                    var task = producer(); // start task
                    item = (task, now);
                    TaskCache[key] = item;
                }
            }
            return await item.task.ConfigureAwait(false); // await task outside lock
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

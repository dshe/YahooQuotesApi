﻿using System;
using System.Collections.Generic;
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
        private readonly Dictionary<TKey, (Instant, Task<TResult>)> TaskCache = new Dictionary<TKey, (Instant, Task<TResult>)>();
        private readonly Duration Duration;

        internal AsyncItemCache(Duration cacheDuration) : this(SystemClock.Instance, cacheDuration) { }
        internal AsyncItemCache(IClock clock, Duration cacheDuration)
        {
            Clock = clock;
            Duration = cacheDuration;
        }

        internal async Task<TResult> Get(TKey key, Func<Task<TResult>> factory)
        {
            (Instant time, Task<TResult> task) item;
            lock (TaskCache)
            {
                var now = Clock.GetCurrentInstant();
                if (!TaskCache.TryGetValue(key, out item) || now - item.time > Duration)
                {
                    var task = factory(); // start task
                    item = (now, task);
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
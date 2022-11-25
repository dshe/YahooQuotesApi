using System.Collections.Generic;
using System.Threading.Tasks;

namespace YahooQuotesApi;

/*
 * TResult - the type of result to be cached
 * TKey - the type of the key used to identify the result
 */
internal sealed class ParallelProducerCache<TKey, TResult> where TKey : notnull
{
    private readonly IClock Clock;
    private readonly Dictionary<TKey, (Task<TResult>, Instant)> TaskCache = new();
    private readonly Duration Duration;

    internal ParallelProducerCache(IClock clock, Duration cacheDuration)
    {
        Duration = cacheDuration;
        Clock = clock;
    }

    internal async Task<TResult> Get(TKey key, Func<Task<TResult>> producer)
    {
        (Task<TResult> task, Instant time) item;
        lock (TaskCache)
        {
            Instant now = Clock.GetCurrentInstant();
            if (!TaskCache.TryGetValue(key, out item) || now - item.time > Duration)
            {
                Task<TResult> task = producer(); // start task
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

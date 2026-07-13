namespace YahooQuotesApi;

/*
 * TResult - the type of result to be cached
 * TKey - the type of the key used to identify the result
 */
internal sealed class ParallelProducerCache<TKey, TResult> where TKey : notnull
{
    private readonly IClock _clock;
    private readonly Dictionary<TKey, (Task<TResult>, Instant)> _taskCache = [];
    private readonly Lock _taskCacheLock = new();
    private readonly Duration _duration;

    internal ParallelProducerCache(IClock clock, Duration cacheDuration)
    {
        _duration = cacheDuration;
        _clock = clock;
    }

    internal async Task<TResult> GetAsync(TKey key, Func<Task<TResult>> producer)
    {
        (Task<TResult> task, Instant time) item;
        lock (_taskCacheLock)
        {
            Instant now = _clock.GetCurrentInstant();
            if (!_taskCache.TryGetValue(key, out item) || now - item.time > _duration)
            {
                Task<TResult> task = producer(); // start task
                item = (task, now);
                _taskCache[key] = item;
            }
        }
        return await item.task.ConfigureAwait(false); // await task outside lock
    }

    internal void Clear()
    {
        lock (_taskCacheLock)
        {
            _taskCache.Clear();
        }
    }
}

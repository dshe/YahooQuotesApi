namespace YahooQuotesApi;

internal sealed class SerialProducerCache<TKey, TResult> : IDisposable where TKey : notnull
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly List<TKey> _buffer = [];
    private readonly Cache<TKey, TResult> _cache;
    private readonly Func<List<TKey>, CancellationToken, Task<Dictionary<TKey, TResult>>> _produce;

    internal SerialProducerCache(IClock clock, Duration cacheDuration, Func<List<TKey>, CancellationToken, Task<Dictionary<TKey, TResult>>> produce)
    {
        _cache = new Cache<TKey, TResult>(clock, cacheDuration);
        _produce = produce;
    }

    internal async Task<Dictionary<TKey, TResult>> GetAsync(HashSet<TKey> keys, CancellationToken ct)
    {
        if (_cache.TryGetAll(keys, out var results))
            return results;
        
        lock (_buffer)
        {
            _buffer.AddRange(keys);
        }

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            List<TKey> items;
            lock (_buffer)
            {
                items = [.. _buffer]; // make copy
                _buffer.Clear();
            }
            if (items.Count != 0)
            {
                results = await _produce(items, ct).ConfigureAwait(false);
                _cache.Add(results);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return _cache.GetAll(keys);
    }

    public void Dispose() => _semaphore.Dispose();
}

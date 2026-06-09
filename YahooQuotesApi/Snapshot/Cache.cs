namespace YahooQuotesApi;

internal sealed class Cache<TKey, TResult> where TKey : notnull
{
    private readonly IClock _clock;
    private readonly Duration _cacheDuration;
    private readonly Dictionary<TKey, (TResult result, Instant time)> _items = [];

    internal Cache(IClock clock, Duration cacheDuration)
    {
        _clock = clock;
        _cacheDuration = cacheDuration;
    }

    internal void Add(Dictionary<TKey, TResult> dict)
    {
        lock (_items)
        {
            Instant now = _clock.GetCurrentInstant();
            foreach (var kvp in dict)
                _items[kvp.Key] = (kvp.Value, now);
        }
    }

    internal bool TryGetAll(HashSet<TKey> keys, out Dictionary<TKey, TResult> results)
    {

        results = new Dictionary<TKey, TResult>(keys.Count); // each request returns a new dictionary
        lock (_items)
        {
            Instant now = _clock.GetCurrentInstant();
            foreach (TKey key in keys)
            {
                // Return false if any key not found, or if any value not null and expired.
                if (!_items.TryGetValue(key, out (TResult value, Instant time) item)
                    || (item.value is not null && (now - item.time) > _cacheDuration))
                {
                    return false;
                }
                results.Add(key, item.value);
            }
        }
        return true;
    }

    internal Dictionary<TKey, TResult> GetAll(HashSet<TKey> keys)
    {
        lock (_items)
        {
            return keys.ToDictionary(k => k, k => _items[k].result);
        }
    }
}

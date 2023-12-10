namespace YahooQuotesApi;

internal sealed class Cache<TKey, TResult> where TKey : notnull
{
    private readonly IClock Clock;
    private readonly Duration CacheDuration;
    private readonly Dictionary<TKey, (TResult result, Instant time)> Items = new();

    internal Cache(IClock clock, Duration cacheDuration)
    {
        Clock = clock;
        CacheDuration = cacheDuration;
    }

    internal void Add(Dictionary<TKey, TResult> dict)
    {
        lock (Items)
        {
            Instant now = Clock.GetCurrentInstant();
            foreach (var kvp in dict)
                Items[kvp.Key] = (kvp.Value, now);
        }
    }

    internal bool TryGetAll(HashSet<TKey> keys, out Dictionary<TKey, TResult> results)
    {

        results = new Dictionary<TKey, TResult>(keys.Count); // each request returns a new dictionary
        lock (Items)
        {
            Instant now = Clock.GetCurrentInstant();
            foreach (TKey key in keys)
            {
                // Return false if any key not found, or if any value not null and expired.
                if (!Items.TryGetValue(key, out (TResult value, Instant time) item)
                    || (item.value is not null && (now - item.time) > CacheDuration))
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
        lock (Items)
        {
            return keys.ToDictionary(k => k, k => Items[k].result);
        }
    }
}

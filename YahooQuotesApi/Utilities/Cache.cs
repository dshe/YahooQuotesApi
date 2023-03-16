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

    internal void Save(Dictionary<TKey, TResult> dict)
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
                if (!Items.TryGetValue(key, out (TResult value, Instant time) item)
                    || (item.value is not null && (now - item.time) > CacheDuration))
                {
                    results.Clear();
                    return false;
                }
                results.Add(key, item.value);
            }
        }
        return true;
    }

    internal Dictionary<TKey, TResult> Get(HashSet<TKey> keys)
    {
        Dictionary<TKey, TResult> dictionary = new(keys.Count);
        lock (Items)
        {
            foreach (TKey key in keys)
                dictionary.Add(key, Items[key].result);
        }
        return dictionary;
    }
}

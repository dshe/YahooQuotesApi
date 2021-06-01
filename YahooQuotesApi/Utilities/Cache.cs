using NodaTime;
using System.Collections.Generic;
using System.Linq;

namespace YahooQuotesApi
{
    internal class Cache<TKey, TResult> where TKey:notnull
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
                var now = Clock.GetCurrentInstant();
                foreach (var kvp in dict)
                    Items[kvp.Key] = (kvp.Value, now);
            }
        }

        internal bool TryGetAll(HashSet<TKey> keys, out Dictionary<TKey, TResult> results)
        {
            results = new Dictionary<TKey, TResult>(keys.Count); // each request returns a new dictionary
            lock (Items)
            {
                var now = Clock.GetCurrentInstant();
                foreach (var key in keys)
                {
                    if (Items.TryGetValue(key, out (TResult value, Instant time) item)
                        && (now - item.time <= CacheDuration || item.value is null))
                        results.Add(key, item.value);
                    else
                    {
                        results.Clear();
                        return false;
                    }
                }
                return true;
            }
        }

        internal Dictionary<TKey, TResult> Get(HashSet<TKey> keys)
        {
            lock (Items)
            {
                return keys.ToDictionary(k => k, k => Items[k].result);
            }
        }
    }
}

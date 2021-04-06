using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;

namespace YahooQuotesApi
{
    internal class AsyncItemsCache<TKey, TResult>
    {
        private readonly SemaphoreWrapper SemaphoreWrapper = new SemaphoreWrapper(1, 1);
        private readonly List<TKey> Pending = new List<TKey>();
        private readonly Cache<TKey, TResult> Cache;
        private readonly Delayer Delayer;
        private readonly Duration Delay;
        private readonly Func<List<TKey>, CancellationToken, Task<Dictionary<TKey, TResult>>> Produce;

        internal AsyncItemsCache(IClock clock, Duration cacheDuration, Duration delay, Func<List<TKey>, CancellationToken, Task<Dictionary<TKey, TResult>>> produce)
        {
            Delayer = new Delayer(clock);
            Cache = new Cache<TKey, TResult>(clock, cacheDuration);
            Delay = delay;
            Produce = produce;
        }

        internal async Task<Dictionary<TKey, TResult>> Get(List<TKey> keys, CancellationToken ct)
        {
            var results = Cache.GetAllElseEmpty(keys);
            if (results.Any())
                return results;

            lock (Pending)
            {
                Pending.AddRange(keys);
                Delayer.Update();
            }

            await SemaphoreWrapper.Wrap<Task>(() => Process(ct));

            return Cache.GetAll(keys);

        }

        private async Task Process(CancellationToken ct)
        {
            while (true)
            {
                await Delayer.Delay(Delay, ct).ConfigureAwait(false);
                var items = new List<TKey>();
                lock (Pending)
                {
                    if (!Pending.Any())
                        return;
                    items.AddRange(Pending);
                    Pending.Clear();
                }
                var dictionary = await Produce(items, ct).ConfigureAwait(false);
                Cache.Store(dictionary);
            }
        }
    }

    internal class Delayer
    {
        private readonly object lockObj = new object();
        private readonly IClock Clock;
        private Instant LastUpdateTime = Instant.MinValue;
        
        internal Delayer(IClock clock) => Clock = clock;
        
        internal void Update()
        {
            lock (lockObj)
            {
                LastUpdateTime = Clock.GetCurrentInstant();
            }
        }
        
        internal async Task Delay(Duration minimumDelay, CancellationToken ct)
        {
            while (true)
            {
                Duration delay = minimumDelay - GetTimeSinceLastUpdate();
                if (delay <= Duration.Zero)
                    break;
                await Task.Delay(delay.ToTimeSpan(), ct).ConfigureAwait(false);
            }
        }
        
        private Duration GetTimeSinceLastUpdate()
        {
            lock (lockObj)
            {
                return Clock.GetCurrentInstant() - LastUpdateTime;
            }
        }
    }
}

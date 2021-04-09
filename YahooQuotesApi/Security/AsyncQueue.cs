using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    internal class AsyncQueue<T>
    {
        private readonly SemaphoreSlim Semaphore = new SemaphoreSlim(0);
        private readonly List<T> Items = new List<T>();
        private readonly int Timeout;

        internal AsyncQueue(int timeout)
        {
            if (timeout < 0)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            Timeout = timeout;
        }

        internal int Count()
        {
            lock (Items)
            {
                return Items.Count;
            }
        }

        internal void Add(IEnumerable<T> items)
        {
            lock (Items)
            {
                Items.AddRange(items);
                Semaphore.Release(); // increment the semaphore counter
            }
        }

        internal async Task<List<T>> RemoveAllAsync(CancellationToken ct = default)
        {
            while (await Semaphore.WaitAsync(Timeout, ct).ConfigureAwait(false))
                continue;
            lock (Items)
            {
                var list = new List<T>(Items);
                Items.Clear();
                return list;
            }
        }
    }
}

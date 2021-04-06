using System;
using System.Threading;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    internal class SemaphoreWrapper
    {
        private readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1);
        internal SemaphoreWrapper(int initialCount, int maxCount) => Semaphore = new SemaphoreSlim(initialCount, maxCount);

        internal async Task Wrap<TResult>(Func<Task> fcn, CancellationToken ct = default)
        {
            await Semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await fcn().ConfigureAwait(false);
            }
            finally
            {
                Semaphore.Release();
            }
        }
        internal async Task<TResult> Wrap<TResult>(Func<Task<TResult>> fcn, CancellationToken ct = default)
        {
            await Semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await fcn().ConfigureAwait(false);
            }
            finally
            {
                Semaphore.Release();
            }
        }
    }
}

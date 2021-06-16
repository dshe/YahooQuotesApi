using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    internal static class TaskEx
    {
        internal static async Task WhenAll(IEnumerable<Task> tasks)
        {
            var allTasks = Task.WhenAll(tasks);

            try
            {
                await allTasks.ConfigureAwait(false);
                return;
            }
            catch
            {
                // ignore
            }

            // throw an aggregate exception
            throw allTasks.Exception ?? throw new InvalidOperationException("impossible!");
        }

        internal static async Task<IEnumerable<T>> WhenAll<T>(IEnumerable<Task<T>> tasks)
        {
            var allTasks = Task.WhenAll(tasks);

            try
            {
                return await allTasks.ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            // throw an aggregate exception
            throw allTasks.Exception ?? throw new InvalidOperationException("impossible!");
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util
{
    /// <summary>
    /// Ensures that an item is only accessed by one thread at a time.
    ///
    /// Use this in place of trying to remeber to lock(foo){} around each place you want to use foo.
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MutexedItem<T>
    {
        readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        readonly T itemWhichCanOnlyBeAccessedByOneThreadAtATime;

        public MutexedItem(T itemWhichCanOnlyBeAccessedByOneThreadAtATime)
        {
            this.itemWhichCanOnlyBeAccessedByOneThreadAtATime = itemWhichCanOnlyBeAccessedByOneThreadAtATime;
        }

        public void DoWithExclusiveAccess(Action<T> action)
        {
            semaphoreSlim.Wait();
            try
            {
                action(itemWhichCanOnlyBeAccessedByOneThreadAtATime);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public async Task DoWithExclusiveAccess(Func<T, Task> action)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                await action(itemWhichCanOnlyBeAccessedByOneThreadAtATime);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public R DoWithExclusiveAccess<R>(Func<T, R> action)
        {
            semaphoreSlim.Wait();
            try
            {
                return action(itemWhichCanOnlyBeAccessedByOneThreadAtATime);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }
    }
}
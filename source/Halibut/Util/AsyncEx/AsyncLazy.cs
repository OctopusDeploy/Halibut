using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util.AsyncEx
{
    /// <summary>
    /// A Lazy implementation with CancellationToken support.
    /// </summary>
    public class AsyncLazy<T>
    {
        readonly Func<CancellationToken, Task<T>> factoryFunc;

        readonly SemaphoreSlim semaphore = new(1, 1);
        volatile bool hasValue;
        T value = default!;

        public AsyncLazy(Func<CancellationToken, Task<T>> factoryFunc)
        {
            this.factoryFunc = factoryFunc;
        }

        public bool HasValue => hasValue;

        public async Task<T> Value(CancellationToken cancellationToken)
        {
            if (hasValue) return value;
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                if (hasValue) return value;
                value = await factoryFunc(cancellationToken);
                hasValue = true;
                return value;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}

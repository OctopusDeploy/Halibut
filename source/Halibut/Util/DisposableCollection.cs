using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Halibut.Util
{
    public class DisposableCollection : IDisposable, IAsyncDisposable
    {

        //Dispose in the reverse order of when they were added so we deal with nested objects correctly.
        readonly ConcurrentStack<IAsyncDisposable> disposables = new();
        bool isDisposed;

        public DisposableCollection()
        {
            
        }

        public DisposableCollection(params IAsyncDisposable[] disposables)
        {
            this.disposables.PushRange(disposables);
        }

        public T Add<T>(T disposable) where T : IDisposable
        {
            if (isDisposed) throw new ObjectDisposedException("Cannot add item for disposal. This collection has already been disposed.");

            if (disposable is IAsyncDisposable asyncDisposable)
            {
                disposables.Push(asyncDisposable);
            }
            else
            {
                disposables.Push(new AsyncDisposer(disposable));
            }

            return disposable;
        }

        public IAsyncDisposable AddAsyncDisposable<T>(T asyncDisposable) where T : IAsyncDisposable
        {
            if (isDisposed) throw new ObjectDisposedException("Cannot add item for disposal. This collection has already been disposed.");

            disposables.Push(asyncDisposable);

            return asyncDisposable;
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (isDisposed) return;
            isDisposed = true;

            var exceptions = new List<Exception>();
            while (!disposables.IsEmpty)
                try
                {
                    if (disposables.TryPop(out var disposable))
                    {
                        await disposable.DisposeAsync();
                    }
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }

            if (exceptions.Any()) throw new AggregateException(exceptions);
        }

        class AsyncDisposer : IAsyncDisposable
        {
            readonly IDisposable disposableItem;

            public AsyncDisposer(IDisposable disposableItem)
            {
                this.disposableItem = disposableItem;
            }

            public async ValueTask DisposeAsync()
            {
                await Task.CompletedTask;

                disposableItem.Dispose();
            }
        }
    }
}
// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Halibut.Tests.Support
{
    public class DisposableCollection : IDisposable
    {
        readonly ConcurrentStack<IDisposable> disposables = new();

        public void Add(IDisposable disposable)
        {
            disposables.Push(disposable);
        }

        public void AddIgnoringDisposalError(IDisposable disposable)
        {
            disposables.Push(new SilentDisposable(disposable));
        }

        public void Dispose()
        {
            var exceptions = new List<Exception>();
            while (!disposables.IsEmpty)
                try
                {
                    if (disposables.TryPop(out var disposable)) disposable.Dispose();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }

            if (exceptions.Any()) throw new AggregateException(exceptions);
        }

        class SilentDisposable : IDisposable
        {
            IDisposable inner;

            public SilentDisposable(IDisposable inner)
            {
                this.inner = inner;
            }

            public void Dispose()
            {
                Try.CatchingError(() => inner.Dispose(), _ => { });
            }
        }
    }
}
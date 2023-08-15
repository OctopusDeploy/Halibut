using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public interface IConnectionPool<TKey, TPooledResource> : IDisposable, IAsyncDisposable
        where TPooledResource : class, IPooledResource 
    {
        int GetTotalConnectionCount();
        TPooledResource Take(TKey endPoint);
        Task<TPooledResource> TakeAsync(TKey endPoint, CancellationToken cancellationToken);
        void Return(TKey endPoint, TPooledResource resource);
        Task ReturnAsync(TKey endPoint, TPooledResource resource, CancellationToken cancellationToken);
        void Clear(TKey key, ILog log = null);
        Task ClearAsync(TKey key, ILog log, CancellationToken cancellationToken);
    }
}
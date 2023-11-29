using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public interface IConnectionPool<TKey, TPooledResource> : IAsyncDisposable
        where TPooledResource : class, IPooledResource
    {
        Task<TPooledResource?> TakeAsync(TKey endPoint, CancellationToken cancellationToken);

        Task ReturnAsync(TKey endPoint, TPooledResource resource, CancellationToken cancellationToken);

        Task ClearAsync(TKey key, ILog log, CancellationToken cancellationToken);
    }
}
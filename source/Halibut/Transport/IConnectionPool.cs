using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public interface IConnectionPool<TKey, TPooledResource> where TPooledResource : class, IPooledResource
    {
        [Obsolete]
        TPooledResource Take(TKey endPoint);
        Task<TPooledResource> TakeAsync(TKey endPoint, CancellationToken cancellationToken);

        [Obsolete]
        void Return(TKey endPoint, TPooledResource resource);
        Task ReturnAsync(TKey endPoint, TPooledResource resource, CancellationToken cancellationToken);

        [Obsolete]
        void Clear(TKey key, ILog log = null);
        Task ClearAsync(TKey key, ILog log, CancellationToken cancellationToken);
    }
}
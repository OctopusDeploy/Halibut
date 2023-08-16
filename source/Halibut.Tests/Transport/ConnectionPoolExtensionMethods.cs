using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Transport;

namespace Halibut.Tests.Transport
{
    public static class ConnectionPoolExtensionMethods
    {
        public static IConnectionPool<TKey, TPooledResource> CreateConnectionPool<TKey, TPooledResource>(this SyncOrAsync syncOrAsync)
            where TPooledResource : class, IPooledResource
        {
            switch (syncOrAsync)
            {
                case SyncOrAsync.Sync:
                    return new ConnectionPool<TKey, TPooledResource>();
                case SyncOrAsync.Async:
                    return new ConnectionPoolAsync<TKey, TPooledResource>();
                default:
                    throw new ArgumentOutOfRangeException(nameof(syncOrAsync), syncOrAsync, null);
            }
        }
        public static async Task Return_SyncOrAsync<TKey, TPooledResource>(
            this IConnectionPool<TKey, TPooledResource> connectionPool, 
            SyncOrAsync syncOrAsync,
            TKey endPoint, 
            TPooledResource resource, 
            CancellationToken cancellationToken)
            where TPooledResource : class, IPooledResource
        {
#pragma warning disable CS0612 // Type or member is obsolete
            await syncOrAsync
                .WhenSync(() => connectionPool.Return(endPoint, resource))
                .WhenAsync(async () => await connectionPool.ReturnAsync(endPoint, resource, cancellationToken));
#pragma warning restore CS0612 // Type or member is obsolete
        }

        public static async Task<TPooledResource> Take_SyncOrAsync<TKey, TPooledResource>(
            this IConnectionPool<TKey, TPooledResource> connectionPool,
            SyncOrAsync syncOrAsync,
            TKey endPoint,
            CancellationToken cancellationToken)
            where TPooledResource : class, IPooledResource
        {
#pragma warning disable CS0612 // Type or member is obsolete
            return await syncOrAsync
                .WhenSync(() => connectionPool.Take(endPoint))
                .WhenAsync(async () => await connectionPool.TakeAsync(endPoint, cancellationToken));
#pragma warning restore CS0612 // Type or member is obsolete
        }
    }
}
using Halibut.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Halibut.Transport
{
    public class ConnectionPoolAsync<TKey, TPooledResource> : IConnectionPool<TKey, TPooledResource>
        where TKey : notnull
        where TPooledResource : class, IPooledResource
    {
        readonly Dictionary<TKey, HashSet<TPooledResource>> pool = new();
        readonly SemaphoreSlim poolLock = new(1, 1);
        
        public async Task<TPooledResource?> TakeAsync(TKey endPoint, CancellationToken cancellationToken)
        {
            using (await poolLock.LockAsync(cancellationToken))
            {
                var connections = GetOrAdd(endPoint);

                while (true)
                {
                    var connection = Take(connections);

                    if (connection == null || !connection.HasExpired())
                        return connection;

                    await DestroyConnectionAsync(connection, null);
                }
            }
        }

        public async Task ReturnAsync(TKey endPoint, TPooledResource resource, CancellationToken cancellationToken)
        {
            using (await poolLock.LockAsync(cancellationToken))
            {
                var connections = GetOrAdd(endPoint);
                connections.Add(resource);
                resource.NotifyUsed();

                while (connections.Count > 5)
                {
                    var connection = Take(connections);
                    await DestroyConnectionAsync(connection, null);
                }
            }
        }

        public async Task ClearAsync(TKey key, ILog log, CancellationToken cancellationToken)
        {
            using (await poolLock.LockAsync(cancellationToken))
            {
                if (!pool.TryGetValue(key, out var connections))
                    return;

                foreach (var connection in connections)
                {
                    await DestroyConnectionAsync(connection, log);
                }

                connections.Clear();
                pool.Remove(key);
            }
        }

        public async ValueTask DisposeAsync()
        {
            using (await poolLock.LockAsync())
            {
                foreach (var connection in pool.SelectMany(kv => kv.Value))
                {
                    await DestroyConnectionAsync(connection, null);
                }

                pool.Clear();
            }
        }

        TPooledResource? Take(HashSet<TPooledResource> connections)
        {
            if (connections.Count == 0)
                return null;

            var connection = connections.First();
            connections.Remove(connection);
            return connection;
        }

        HashSet<TPooledResource> GetOrAdd(TKey endPoint)
        {
            if (!pool.TryGetValue(endPoint, out var connections))
            {
                connections = new HashSet<TPooledResource>();
                pool.Add(endPoint, connections);
            }

            return connections;
        }
        
        static async Task DestroyConnectionAsync(TPooledResource? connection, ILog? log)
        {
            try
            {
                if (connection is not null)
                {
                    await connection.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                log?.WriteException(EventType.Error, "Exception disposing connection from pool", ex);
            }
        }
    }
}
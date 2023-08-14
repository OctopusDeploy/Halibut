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
        where TPooledResource : class, IPooledResource
    {
        readonly Dictionary<TKey, HashSet<TPooledResource>> pool = new();
        readonly SemaphoreSlim poolLock = new(1, 1);

        public int GetTotalConnectionCount()
        {
            using (poolLock.Lock())
            {
                return pool.Values.Sum(v => v.Count);
            }
        }

        public TPooledResource Take(TKey endPoint)
        {
            using (poolLock.Lock())
            {
                var connections = GetOrAdd(endPoint);

                while (true)
                {
                    var connection = Take(connections);

                    if (connection == null || !connection.HasExpired())
                        return connection;

                    DestroyConnection(connection, null);
                }
            }
        }

        public async Task<TPooledResource> TakeAsync(TKey endPoint, CancellationToken cancellationToken)
        {
            using (await poolLock.LockAsync(cancellationToken))
            {
                var connections = GetOrAdd(endPoint);

                while (true)
                {
                    var connection = Take(connections);

                    if (connection == null || !connection.HasExpired())
                        return connection;

                    await DestroyConnectionAsync(connection, null, cancellationToken);
                }
            }
        }

        public void Return(TKey endPoint, TPooledResource resource)
        {
            using (poolLock.Lock())
            {
                var connections = GetOrAdd(endPoint);
                connections.Add(resource);
                resource.NotifyUsed();

                while (connections.Count > 5)
                {
                    var connection = Take(connections);
                    DestroyConnection(connection, null);
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
                    await DestroyConnectionAsync(connection, null, cancellationToken);
                }
            }
        }

        public void Clear(TKey key, ILog log = null)
        {
            using (poolLock.Lock())
            {
                if (!pool.TryGetValue(key, out var connections))
                    return;

                foreach (var connection in connections)
                {
                    DestroyConnection(connection, log);
                }

                connections.Clear();
                pool.Remove(key);
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
                    await DestroyConnectionAsync(connection, log, cancellationToken);
                }

                connections.Clear();
                pool.Remove(key);
            }
        }
        
        public void Dispose()
        {
            using (poolLock.Lock())
            {
                foreach (var connection in pool.SelectMany(kv => kv.Value))
                {
                    DestroyConnection(connection, null);
                }

                pool.Clear();
            }
        }

        TPooledResource Take(HashSet<TPooledResource> connections)
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

        void DestroyConnection(TPooledResource connection, ILog log)
        {
            try
            {
                connection?.Dispose();
            }
            catch (Exception ex)
            {
                log?.WriteException(EventType.Error, "Exception disposing connection from pool", ex);
            }
        }

        async Task DestroyConnectionAsync(TPooledResource connection, ILog log, CancellationToken cancellationToken)
        {
            // TODO - ASYNC ME UP! This will come when the async disposal story is done
            await Task.CompletedTask;

            try
            {
                connection?.Dispose();
            }
            catch (Exception ex)
            {
                log?.WriteException(EventType.Error, "Exception disposing connection from pool", ex);
            }
        }
    }
}
using Halibut.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport
{
    public class ConnectionPool<TKey, TPooledResource> : IDisposable, IAsyncDisposable
        where TPooledResource : class, IPooledResource
    {
        readonly Dictionary<TKey, HashSet<TPooledResource>> pool = new();
        readonly SemaphoreSlim poolLock = new(1, 1);

        public int GetTotalConnectionCount()
        {
            poolLock.Lock
            lock (pool)
            {
                return pool.Values.Sum(v => v.Count);
            }
        }

        public TPooledResource Take(TKey endPoint)
        {
            lock (pool)
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

        public void Return(TKey endPoint, TPooledResource resource)
        {
            lock (pool)
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

        public void Clear(TKey key, ILog log = null)
        {
            lock (pool)
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

        public void Dispose()
        {
            lock (pool)
            {
                foreach (var connection in pool.SelectMany(kv => kv.Value))
                {
                    DestroyConnection(connection, null);
                }

                pool.Clear();
            }
        }
        
        public async ValueTask DisposeAsync()
        {
            lock (pool)
            {
                foreach (var connection in pool.SelectMany(kv => kv.Value))
                {
                    await DestroyConnectionAsync(connection, null);
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

        async Task DestroyConnectionAsync(TPooledResource connection, ILog log)
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
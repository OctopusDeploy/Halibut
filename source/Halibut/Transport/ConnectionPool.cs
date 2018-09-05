using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.Serialization;
using Halibut.Diagnostics;
using Halibut.Logging;

namespace Halibut.Transport
{
    public class ConnectionPool<TKey, TPooledResource>
        where TPooledResource : class, IPooledResource
    {
        readonly ConcurrentDictionary<TKey, ConcurrentDictionary<TPooledResource, byte>> pool = new ConcurrentDictionary<TKey, ConcurrentDictionary<TPooledResource, byte>>();

        public int GetTotalConnectionCount()
        {
            return pool.Values.Sum(v => v.Count);
        }

        public TPooledResource Take(TKey endPoint)
        {
            while (true)
            {
                var connections = pool.GetOrAdd(endPoint, i => new ConcurrentDictionary<TPooledResource, byte>());
                AtomicTake(connections, out var connection);

                if (connection == null || !connection.HasExpired())
                    return connection;

                connection.Dispose();
            }
        }



        public void Return(TKey key, TPooledResource resource)
        {
            var resources = pool.GetOrAdd(key, i => new ConcurrentDictionary<TPooledResource, byte>());
            resources.TryAdd(resource, 0);
            resource.NotifyUsed();

            while (resources.Count > 5)
            {
                if (AtomicTake(resources, out var dispose))
                {
                    dispose.Dispose();
                }
            }
        }

        public void Clear(TKey key, Diagnostics.ILog log = null)
        {
            if (!pool.TryRemove(key, out var connections))
                return;

            foreach (var connection in connections.Keys)
            {
                connection.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var key in pool.Keys)
            {
                Clear(key);
            }
        }

        private bool AtomicTake(ConcurrentDictionary<TPooledResource, byte> connections, out TPooledResource resource)
        {
            lock (connections)
            {
                resource = null;
                if (connections.Count == 0)
                {
                    return false;
                }

                var key = connections.Keys.First();
                var result = connections.TryRemove(key, out var ignore);
                if (result)
                {
                    resource = key;
                }
                return result;
            }
        }
    }
}
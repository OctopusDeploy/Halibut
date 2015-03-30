using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Halibut.Transport
{
    public class ConnectionPool<TKey, TPooledResource> 
        where TPooledResource : class, IPooledResource
    {
        readonly ConcurrentDictionary<TKey, ConcurrentBag<TPooledResource>> pool = new ConcurrentDictionary<TKey, ConcurrentBag<TPooledResource>>();

        public int GetTotalConnectionCount()
        {
            return pool.ToArray().Sum(p => p.Value.Count);
        }

        public TPooledResource Take(TKey endPoint)
        {
            while (true)
            {
                var connections = pool.GetOrAdd(endPoint, i => new ConcurrentBag<TPooledResource>());
                TPooledResource connection;
                connections.TryTake(out connection);

                if (connection == null || !connection.HasExpired()) 
                    return connection;
                
                connection.Dispose();
            }
        }

        public void Return(TKey key, TPooledResource resource)
        {
            var resources = pool.GetOrAdd(key, i => new ConcurrentBag<TPooledResource>());
            resources.Add(resource);
            resource.NotifyUsed();

            while (resources.Count > 5)
            {
                TPooledResource dispose;
                if (resources.TryTake(out dispose))
                {
                    dispose.Dispose();
                }
            }
        }

        public void Dispose()
        {
            var keys = pool.Keys.ToList();

            foreach (var key in keys)
            {
                ConcurrentBag<TPooledResource> connections;
                if (pool.TryRemove(key, out connections))
                {
                    while (connections.Count > 0)
                    {
                        TPooledResource dispose;
                        if (connections.TryTake(out dispose))
                        {
                            dispose.Dispose();
                        }
                    }
                }
            }
        }
    }
}
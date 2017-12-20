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

        public void Clear(TKey key, Diagnostics.ILog log = null)
        {
            ConcurrentBag<TPooledResource> connections;
            if (!pool.TryRemove(key, out connections))
                return;

            // this looks like it should be connections.Count > 0, however setting it to 0
            // led to an uptick in users seeing Halibut stacktraces in
            // in their logs. This may have just uncovered another issue.
            // We are returning to 1 in Dec 2016 and will monitor. Are
            // we disposing a connection that is in use somehow? Adding the same connection
            // twice?
            var generator = new ObjectIDGenerator();
            while (connections.Count > 1)
            {
                TPooledResource connection;
                if (connections.TryTake(out connection))
                {
                    if (log != null)
                    {
                        bool firstTime;
                        generator.GetId(connection, out firstTime);
                        if (!firstTime)
                        {
                            log.Write(EventType.Error, "Duplicate connection found in conenction pool");
                        }
                    }
                    connection.Dispose();
                }
            }
        }

        public void Dispose()
        {
            var keys = pool.Keys.ToList();

            foreach (var key in keys)
            {
                Clear(key);
            }
        }
    }
}
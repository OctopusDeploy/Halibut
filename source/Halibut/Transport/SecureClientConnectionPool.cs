using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Halibut.Transport
{
    public class SecureClientConnectionPool
    {
        readonly ConcurrentDictionary<ServiceEndPoint, ConcurrentBag<SecureConnection>> pool = new ConcurrentDictionary<ServiceEndPoint, ConcurrentBag<SecureConnection>>();

        public SecureConnection Take(ServiceEndPoint endPoint)
        {
            var connections = pool.GetOrAdd(endPoint, i => new ConcurrentBag<SecureConnection>());
            SecureConnection connection;
            connections.TryTake(out connection);
            return connection;
        }

        public void Return(ServiceEndPoint endPoint, SecureConnection connection)
        {
            var connections = pool.GetOrAdd(endPoint, i => new ConcurrentBag<SecureConnection>());
            connections.Add(connection);

            while (connections.Count > 5)
            {
                SecureConnection dispose;
                if (connections.TryTake(out dispose))
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
                ConcurrentBag<SecureConnection> connections;
                if (pool.TryRemove(key, out connections))
                {
                    while (connections.Count > 0)
                    {
                        SecureConnection dispose;
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
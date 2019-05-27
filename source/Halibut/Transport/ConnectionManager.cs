using System;
using System.Collections.Generic;
using System.Linq;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class ConnectionManager : IDisposable
    {
        readonly ConnectionPool<ServiceEndPoint, IConnection> pool = new ConnectionPool<ServiceEndPoint, IConnection>();
        readonly Dictionary<ServiceEndPoint, HashSet<IConnection>> activeConnections = new Dictionary<ServiceEndPoint, HashSet<IConnection>>();

        public bool IsDisposed { get; private set; }

        public IConnection AcquireConnection(IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log)
        {
            lock (activeConnections)
            {
                var connection = pool.Take(serviceEndpoint) ?? CreateConnection(connectionFactory, serviceEndpoint, log);
                AddConnectionToActiveConnections(serviceEndpoint, connection);

                return connection;
            }
        }

        IConnection CreateConnection(IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log)
        {
            var connection = connectionFactory.EstablishNewConnection(serviceEndpoint, log);

            return new DisposableNotifierConnection(connection, OnConnectionDisposed);
        }

        void AddConnectionToActiveConnections(ServiceEndPoint serviceEndpoint, IConnection connection)
        {
            if (activeConnections.TryGetValue(serviceEndpoint, out var connections))
            {
                connections.Add(connection);
            }
            else
            {
                connections = new HashSet<IConnection> {connection};
                activeConnections.Add(serviceEndpoint, connections);
            }
        }

        public void ReleaseConnection(ServiceEndPoint serviceEndpoint, IConnection connection)
        {
            lock (activeConnections)
            {
                pool.Return(serviceEndpoint, connection);
                if (activeConnections.TryGetValue(serviceEndpoint, out var connections))
                {
                    connections.Remove(connection);
                }
            }
        }

        public void ClearPooledConnections(ServiceEndPoint serviceEndPoint, ILog log)
        {
            lock (activeConnections)
            {
                pool.Clear(serviceEndPoint, log);
            }
        }

        static IConnection[] NoConnections = new IConnection[0];
        public IReadOnlyCollection<IConnection> GetActiveConnections(ServiceEndPoint serviceEndPoint)
        {
            lock (activeConnections)
            {
                if (activeConnections.TryGetValue(serviceEndPoint, out var value))
                {
                    return value.ToArray();
                }
            }

            return NoConnections;
        }

        public void Disconnect(ServiceEndPoint serviceEndPoint, ILog log)
        {
            ClearPooledConnections(serviceEndPoint, log);
            ClearActiveConnections(serviceEndPoint, log);
        }

        public void Dispose()
        {
            pool.Dispose();
            lock (activeConnections)
            {
                var connectionsToDispose = activeConnections.SelectMany(kv => kv.Value).ToArray();
                foreach (var connection in connectionsToDispose)
                {
                    SafelyDisposeConnection(connection, null);
                }
            }

            IsDisposed = true;
        }


        void ClearActiveConnections(ServiceEndPoint serviceEndPoint, ILog log)
        {
            lock (activeConnections)
            {
                if (activeConnections.TryGetValue(serviceEndPoint, out var activeConnectionsForEndpoint))
                {
                    foreach (var connection in activeConnectionsForEndpoint)
                    {
                        SafelyDisposeConnection(connection, log);
                    }
                }
            }
        }

        void OnConnectionDisposed(IConnection connection)
        {
            lock (activeConnections)
            {
                var setsContainingConnection = activeConnections.Where(c => c.Value.Contains(connection)).ToList();
                var setsToRemoveCompletely = setsContainingConnection.Where(c => c.Value.Count == 1).ToList();
                foreach (var setContainingConnection in setsContainingConnection.Except(setsToRemoveCompletely))
                {
                    setContainingConnection.Value.Remove(connection);
                }

                foreach (var setToRemoveCompletely in setsToRemoveCompletely)
                {
                    activeConnections.Remove(setToRemoveCompletely.Key);
                }
            }
        }

        static void SafelyDisposeConnection(IConnection connection, ILog log)
        {
            try
            {
                connection?.Dispose();
            }
            catch (Exception ex)
            {
                log?.WriteException(EventType.Error, "Exception disposing connection", ex);
            }
        }

        class DisposableNotifierConnection : IConnection
        {
            readonly IConnection connectionImplementation;
            readonly Action<IConnection> onDisposed;

            public DisposableNotifierConnection(IConnection connectionImplementation, Action<IConnection> onDisposed)
            {
                this.connectionImplementation = connectionImplementation;
                this.onDisposed = onDisposed;
            }

            public void Dispose()
            {
                try
                {
                    connectionImplementation.Dispose();
                }
                finally
                {
                    onDisposed(this);
                }
            }

            public void NotifyUsed()
            {
                connectionImplementation.NotifyUsed();
            }

            public bool HasExpired()
            {
                return connectionImplementation.HasExpired();
            }

            public MessageExchangeProtocol Protocol => connectionImplementation.Protocol;
        }
    }
}
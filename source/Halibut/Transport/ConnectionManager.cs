using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class ConnectionManager : IDisposable
    {
        readonly ConnectionPool<ServiceEndPoint, IConnection> pool = new();
        readonly Dictionary<ServiceEndPoint, HashSet<IConnection>> activeConnections = new();

        public bool IsDisposed { get; private set; }

        [Obsolete]
        public IConnection AcquireConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            var openableConnection = GetConnection(exchangeProtocolBuilder, connectionFactory, serviceEndpoint, log, cancellationToken);
            openableConnection.Item2(); // Since this involves IO, this should never be done inside a lock
            return openableConnection.Item1;
        }

        public async Task<IConnection> AcquireConnectionAsync(ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            // TODO - ASYNC ME UP!
            await Task.CompletedTask;

#pragma warning disable CS0612
            return AcquireConnection(exchangeProtocolBuilder, connectionFactory, serviceEndpoint, log, cancellationToken);
#pragma warning restore CS0612
        }

        // Connection is Lazy instantiated, so it is safe to use. If you need to wait for it to open (eg for error handling, an openConnection method is provided)
        // For existing open connections, the openConnection method does nothing
        Tuple<IConnection, Action> GetConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            lock (activeConnections)
            {
                var existingConnectionFromPool = pool.Take(serviceEndpoint);
                var openableConnection = existingConnectionFromPool != null 
                    ? Tuple.Create<IConnection, Action>(existingConnectionFromPool, () => { }) // existing connections from the pool are already open
                    : CreateNewConnection(exchangeProtocolBuilder, connectionFactory, serviceEndpoint, log, cancellationToken);
                AddConnectionToActiveConnections(serviceEndpoint, openableConnection.Item1);
                return openableConnection;
            }
        }

        Tuple<IConnection, Action> CreateNewConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            var lazyConnection = new Lazy<IConnection>(() => connectionFactory.EstablishNewConnection(exchangeProtocolBuilder, serviceEndpoint, log, cancellationToken));
            var connection = new DisposableNotifierConnection(lazyConnection, OnConnectionDisposed);
            return Tuple.Create<IConnection, Action>(connection, () =>
            {
                // ReSharper disable once UnusedVariable
                var c = lazyConnection.Value;
            });
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
                    foreach (var connection in activeConnectionsForEndpoint.ToArray())
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
            readonly Lazy<IConnection> connection;
            readonly Action<IConnection> onDisposed;

            public DisposableNotifierConnection(Lazy<IConnection> connection, Action<IConnection> onDisposed)
            {
                this.connection = connection;
                this.onDisposed = onDisposed;
            }

            public void Dispose()
            {
                try
                {
                    connection.Value.Dispose();
                }
                finally
                {
                    onDisposed(this);
                }
            }

            public void NotifyUsed()
            {
                connection.Value.NotifyUsed();
            }

            public bool HasExpired()
            {
                return connection.Value.HasExpired();
            }

            public MessageExchangeProtocol Protocol => connection.Value.Protocol;
        }
    }
}
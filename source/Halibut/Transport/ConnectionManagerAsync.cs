using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Nito.AsyncEx;

namespace Halibut.Transport
{
    public class ConnectionManagerAsync : IConnectionManager
    {
        readonly ConnectionPoolAsync<ServiceEndPoint, IConnection> pool = new();
        readonly Dictionary<ServiceEndPoint, HashSet<IConnection>> activeConnections = new();

        // We have separate locks for connections in general (including the pool) vs specifically activeConnections.
        // This is because disposing calls OnConnectionDisposed, which causes deadlocks if we are not careful.
        // I.e. use connectionsLock when interacting with anything to do with connections (e.g. the pool, or activeConnections),
        //      but use activeConnectionsLock AND connectionsLock when specifically interacting with activeConnections.
        readonly SemaphoreSlim connectionsLock = new(1, 1);
        readonly SemaphoreSlim activeConnectionsLock = new(1, 1);
        

        public bool IsDisposed { get; private set; }
        
        public void Dispose()
        {
            pool.Dispose();
            using (connectionsLock.Lock())
            {
                IConnection[] connectionsToDispose;
                using (activeConnectionsLock.Lock())
                {
                    connectionsToDispose = activeConnections.SelectMany(kv => kv.Value).ToArray();
                }

                foreach (var connection in connectionsToDispose)
                {
                    SafelyDisposeConnection(connection, null);
                }
            }

            IsDisposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            await pool.DisposeAsync();
            using (await connectionsLock.LockAsync())
            {
                IConnection[] connectionsToDispose;
                using (await activeConnectionsLock.LockAsync())
                {
                    connectionsToDispose = activeConnections.SelectMany(kv => kv.Value).ToArray();
                }

                foreach (var connection in connectionsToDispose)
                {
                    await SafelyDisposeConnectionAsync(connection, null);
                }
            }

            IsDisposed = true;
        }

        [Obsolete]
        public IConnection AcquireConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            Tuple<IConnection, Action> openableConnection;
            using (connectionsLock.Lock())
            {
                var existingConnectionFromPool = pool.Take(serviceEndpoint);
                openableConnection = existingConnectionFromPool != null
                    ? Tuple.Create<IConnection, Action>(existingConnectionFromPool, () => { }) // existing connections from the pool are already open
                    : CreateNewConnection(exchangeProtocolBuilder, connectionFactory, serviceEndpoint, log, cancellationToken);

                using (activeConnectionsLock.Lock())
                {
                    AddConnectionToActiveConnections(serviceEndpoint, openableConnection.Item1);
                }
            }

            openableConnection.Item2(); // Since this involves IO, this should never be done inside a lock
            return openableConnection.Item1;
        }

        public async Task<IConnection> AcquireConnectionAsync(ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            using (await connectionsLock.LockAsync(cancellationToken))
            {
                var existingConnectionFromPool = await pool.TakeAsync(serviceEndpoint, cancellationToken);
                if (existingConnectionFromPool != null)
                {
                    using (await activeConnectionsLock.LockAsync(cancellationToken))
                    {
                        AddConnectionToActiveConnections(serviceEndpoint, existingConnectionFromPool);
                    }

                    return existingConnectionFromPool;
                }
            }

            var connection = await CreateNewConnectionWithIoAsync(exchangeProtocolBuilder, connectionFactory, serviceEndpoint, log, cancellationToken);

            //Do as a separate lock so that we do not lock on IO from creating a new connection.
            using (await connectionsLock.LockAsync(cancellationToken))
            {
                using (await activeConnectionsLock.LockAsync(cancellationToken))
                {
                    AddConnectionToActiveConnections(serviceEndpoint, connection);
                }
            }

            return connection;
        }
        
        [Obsolete]
        Tuple<IConnection, Action> CreateNewConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            var lazyConnection = new Lazy<IConnection>(() => connectionFactory.EstablishNewConnection(exchangeProtocolBuilder, serviceEndpoint, log, cancellationToken));
            var connection = new DisposableNotifierConnection(lazyConnection, OnConnectionDisposed, OnConnectionDisposedAsync);
            return Tuple.Create<IConnection, Action>(connection, () =>
            {
                // ReSharper disable once UnusedVariable
                var c = lazyConnection.Value;
            });
        }
        
        async Task<IConnection> CreateNewConnectionWithIoAsync(ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            var connection = await connectionFactory.EstablishNewConnectionAsync(exchangeProtocolBuilder, serviceEndpoint, log, cancellationToken);
            return new DisposableNotifierConnection(new Lazy<IConnection>(() => connection), OnConnectionDisposed, OnConnectionDisposedAsync);
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

        [Obsolete]
        public void ReleaseConnection(ServiceEndPoint serviceEndpoint, IConnection connection)
        {
            using (connectionsLock.Lock())
            {
                pool.Return(serviceEndpoint, connection);
                using (activeConnectionsLock.Lock())
                {
                    if (activeConnections.TryGetValue(serviceEndpoint, out var connections))
                    {
                        connections.Remove(connection);
                    }
                }
            }
        }

        public async Task ReleaseConnectionAsync(ServiceEndPoint serviceEndpoint, IConnection connection, CancellationToken cancellationToken)
        {
            using (await connectionsLock.LockAsync(cancellationToken))
            {
                await pool.ReturnAsync(serviceEndpoint, connection, cancellationToken);
                using (await activeConnectionsLock.LockAsync(cancellationToken))
                {
                    if (activeConnections.TryGetValue(serviceEndpoint, out var connections))
                    {
                        connections.Remove(connection);
                    }
                }
            }
        }

        [Obsolete]
        public void ClearPooledConnections(ServiceEndPoint serviceEndPoint, ILog log)
        {
            using (connectionsLock.Lock())
            {
                pool.Clear(serviceEndPoint, log);
            }
        }

        public async Task ClearPooledConnectionsAsync(ServiceEndPoint serviceEndPoint, ILog log, CancellationToken cancellationToken)
        {
            using (await connectionsLock.LockAsync(cancellationToken))
            {
                await pool.ClearAsync(serviceEndPoint, log, cancellationToken);
            }
        }
        
        public IReadOnlyCollection<IConnection> GetActiveConnections(ServiceEndPoint serviceEndPoint)
        {
            using (connectionsLock.Lock())
            {
                using (activeConnectionsLock.Lock())
                {
                    if (activeConnections.TryGetValue(serviceEndPoint, out var value))
                    {
                        return value.ToArray();
                    }
                }
            }

            return Array.Empty<IConnection>();
        }

        [Obsolete]
        public void Disconnect(ServiceEndPoint serviceEndPoint, ILog log)
        {
            using (connectionsLock.Lock())
            {
                pool.Clear(serviceEndPoint, log);
                var toDelete = new List<IConnection>();
                using (activeConnectionsLock.Lock())
                {
                    if (activeConnections.TryGetValue(serviceEndPoint, out var activeConnectionsForEndpoint))
                    {
                        toDelete = activeConnectionsForEndpoint.ToList();
                    }
                }

                foreach (var connection in toDelete)
                {
                    SafelyDisposeConnection(connection, log);
                }
            }
        }

        public async Task DisconnectAsync(ServiceEndPoint serviceEndPoint, ILog log, CancellationToken cancellationToken)
        {
            using (await connectionsLock.LockAsync(cancellationToken))
            {
                await pool.ClearAsync(serviceEndPoint, log, cancellationToken);
                var toDelete = new List<IConnection>();
                using (await activeConnectionsLock.LockAsync(cancellationToken))
                {
                    if (activeConnections.TryGetValue(serviceEndPoint, out var activeConnectionsForEndpoint))
                    {
                        toDelete = activeConnectionsForEndpoint.ToList();
                    }
                }

                foreach (var connection in toDelete)
                {
                    await SafelyDisposeConnectionAsync(connection, log);
                }
            }
        }
        
        void OnConnectionDisposed(IConnection connection)
        {
            // If we are not careful, we can introduce a deadlock. Time this out just in case we ever accidentally introduce one.
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            using (activeConnectionsLock.Lock(cts.Token))
            {
                RemoveFromActiveConnections(connection);
            }
        }

        async Task OnConnectionDisposedAsync(IConnection connection)
        {
            // If we are not careful, we can introduce a deadlock. Time this out just in case we ever accidentally introduce one.
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            using (await activeConnectionsLock.LockAsync(cts.Token))
            {
                RemoveFromActiveConnections(connection);
            }
        }

        void RemoveFromActiveConnections(IConnection connection)
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

        static async Task SafelyDisposeConnectionAsync(IConnection connection, ILog log)
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
                log?.WriteException(EventType.Error, "Exception disposing connection", ex);
            }
        }

        class DisposableNotifierConnection : IConnection
        {
            readonly Lazy<IConnection> connection;
            readonly Action<IConnection> onDisposed;
            readonly Func<IConnection, Task> onDisposedAsync;

            public DisposableNotifierConnection(
                Lazy<IConnection> connection, 
                Action<IConnection> onDisposed,
                Func<IConnection, Task> onDisposedAsync)
            {
                this.connection = connection;
                this.onDisposed = onDisposed;
                this.onDisposedAsync = onDisposedAsync;
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


            public async ValueTask DisposeAsync()
            {
                try
                {
                    await connection.Value.DisposeAsync();
                }
                finally
                {
                    await onDisposedAsync(this);
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
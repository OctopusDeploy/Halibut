using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Transport;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Transport
{
    public static class ConnectionManagerExtensionMethods
    {
        public static IConnectionManager CreateConnectionManager(this SyncOrAsync syncOrAsync)
        {
            switch (syncOrAsync)
            {
                case SyncOrAsync.Sync:
                    return new ConnectionManager();
                case SyncOrAsync.Async:
                    return new ConnectionManagerAsync(new HalibutTimeoutsAndLimits());
                default:
                    throw new ArgumentOutOfRangeException(nameof(syncOrAsync), syncOrAsync, null);
            }
        }

        public static async Task<IConnection> AcquireConnection_SyncOrAsync(this IConnectionManager connectionManager, SyncOrAsync syncOrAsync, ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndPoint, ILog log, CancellationToken cancellationToken)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            return await syncOrAsync
                .WhenSync(() => connectionManager.AcquireConnection(exchangeProtocolBuilder, connectionFactory, serviceEndPoint, log, cancellationToken))
                .WhenAsync(async () => await connectionManager.AcquireConnectionAsync(exchangeProtocolBuilder, connectionFactory, serviceEndPoint, log, cancellationToken));
#pragma warning restore CS0612 // Type or member is obsolete
        }

        public static async Task ReleaseConnection_SyncOrAsync(this IConnectionManager connectionManager, SyncOrAsync syncOrAsync, ServiceEndPoint serviceEndpoint, IConnection connection, CancellationToken cancellationToken)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            await syncOrAsync
                .WhenSync(() => connectionManager.ReleaseConnection(serviceEndpoint, connection))
                .WhenAsync(async () => await connectionManager.ReleaseConnectionAsync(serviceEndpoint, connection, cancellationToken));
#pragma warning restore CS0612 // Type or member is obsolete
        }

        public static async Task Disconnect_SyncOrAsync(this IConnectionManager connectionManager, SyncOrAsync syncOrAsync, ServiceEndPoint serviceEndPoint, ILog? log, CancellationToken cancellationToken)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            await syncOrAsync
                .WhenSync(() => connectionManager.Disconnect(serviceEndPoint, log))
                .WhenAsync(async () => await connectionManager.DisconnectAsync(serviceEndPoint, log, cancellationToken));
#pragma warning restore CS0612 // Type or member is obsolete
        }

        public static async Task ClearPooledConnections_SyncOrAsync(this IConnectionManager connectionManager, SyncOrAsync syncOrAsync, ServiceEndPoint serviceEndPoint, ILog? log, CancellationToken cancellationToken)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            await syncOrAsync
                .WhenSync(() => connectionManager.ClearPooledConnections(serviceEndPoint, log))
                .WhenAsync(async () => await connectionManager.ClearPooledConnectionsAsync(serviceEndPoint, log, cancellationToken));
#pragma warning restore CS0612 // Type or member is obsolete
        }
    }
}
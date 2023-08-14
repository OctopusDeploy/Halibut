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
        public static async Task<IConnection> AcquireConnection_SyncOrAsync(this ConnectionManager connectionManager, SyncOrAsync syncOrAsync, ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndPoint, ILog log, CancellationToken cancellationToken)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            return await syncOrAsync
                .WhenSync(() => connectionManager.AcquireConnection(exchangeProtocolBuilder, connectionFactory, serviceEndPoint, log, cancellationToken))
                .WhenAsync(async () => await connectionManager.AcquireConnectionAsync(exchangeProtocolBuilder, connectionFactory, serviceEndPoint, new HalibutTimeoutsAndLimits(), log, cancellationToken));
#pragma warning restore CS0612 // Type or member is obsolete
        }
    }
}
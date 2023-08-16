using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public interface IConnectionManager : IDisposable
    {
        bool IsDisposed { get; }
        [Obsolete]
        IConnection AcquireConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken);
        Task<IConnection> AcquireConnectionAsync(ExchangeProtocolBuilder exchangeProtocolBuilder, IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken);
        [Obsolete]
        void ReleaseConnection(ServiceEndPoint serviceEndpoint, IConnection connection);
        Task ReleaseConnectionAsync(ServiceEndPoint serviceEndpoint, IConnection connection, CancellationToken cancellationToken);
        [Obsolete]
        void ClearPooledConnections(ServiceEndPoint serviceEndPoint, ILog log);
        Task ClearPooledConnectionsAsync(ServiceEndPoint serviceEndPoint, ILog log, CancellationToken cancellationToken);
        IReadOnlyCollection<IConnection> GetActiveConnections(ServiceEndPoint serviceEndPoint);
        [Obsolete]
        void Disconnect(ServiceEndPoint serviceEndPoint, ILog log);
        Task DisconnectAsync(ServiceEndPoint serviceEndPoint, ILog log, CancellationToken cancellationToken);
    }
}
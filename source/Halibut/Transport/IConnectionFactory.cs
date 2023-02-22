using System.Threading;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public interface IConnectionFactory
    {
        IConnection EstablishNewConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, ServiceEndPoint serviceEndpoint, HalibutTimeouts halibutTimeouts, ILog log);
        IConnection EstablishNewConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, ServiceEndPoint serviceEndpoint, HalibutTimeouts halibutTimeouts, ILog log, CancellationToken cancellationToken);
    }
}
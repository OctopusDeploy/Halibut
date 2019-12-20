using System;
using System.Threading;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public interface ISecureClient
    {
        ServiceEndPoint ServiceEndpoint { get; }
        void ExecuteTransaction(Action<MessageExchangeProtocol> protocolHandler);
        void ExecuteTransaction(Action<MessageExchangeProtocol> protocolHandler, CancellationToken cancellationToken);
    }
}
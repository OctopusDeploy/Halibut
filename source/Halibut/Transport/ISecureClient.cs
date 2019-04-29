using System;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public interface ISecureClient
    {
        ServiceEndPoint ServiceEndpoint { get; }
        void ExecuteTransaction(Func<MessageExchangeProtocol, bool> protocolHandler);
    }
}
using System;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public interface ISecureClient : IDisposable
    {
        ServiceEndPoint ServiceEndpoint { get; }
        void ExecuteTransaction(Action<MessageExchangeProtocol> protocolHandler);
    }
}
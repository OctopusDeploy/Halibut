using System;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    interface ISecureClient
    {
        ServiceEndPoint ServiceEndpoint { get; }
        void ExecuteTransaction(Action<MessageExchangeProtocol> protocolHandler);
    }

    interface ISecurePollingClient : ISecureClient
    {
        
    }
}
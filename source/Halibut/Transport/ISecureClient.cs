using System;
using System.Threading;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public interface ISecureClient
    {
        ServiceEndPoint ServiceEndpoint { get; }
        void ExecuteTransaction(ExchangeAction protocolHandler);
        void ExecuteTransaction(ExchangeAction protocolHandler, CancellationToken cancellationToken);
    }
}
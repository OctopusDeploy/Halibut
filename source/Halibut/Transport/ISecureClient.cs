using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public interface ISecureClient
    {
        ServiceEndPoint ServiceEndpoint { get; }
        
        Task ExecuteTransaction(ExchangeActionAsync protocolHandler, CancellationToken cancellationToken);
    }
}
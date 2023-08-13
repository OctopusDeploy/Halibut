using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public interface ISecureClient
    {
        ServiceEndPoint ServiceEndpoint { get; }

        [Obsolete]
        void ExecuteTransaction(ExchangeAction protocolHandler, CancellationToken cancellationToken);
        Task ExecuteTransactionAsync(ExchangeActionAsync protocolHandler, RequestCancellationTokens requestCancellationTokens);
    }
}

using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public interface ISecureClient
    {
        ServiceEndPoint ServiceEndpoint { get; }
        Task ExecuteTransactionAsync(ExchangeActionAsync protocolHandler, CancellationToken cancellationToken);
    }
}

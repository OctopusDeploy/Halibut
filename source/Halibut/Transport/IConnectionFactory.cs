using System.Threading;
using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public interface IConnectionFactory
    {
        IConnection EstablishNewConnection(ServiceEndPoint serviceEndpoint, ILog log);
        IConnection EstablishNewConnection(ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken);
    }
}
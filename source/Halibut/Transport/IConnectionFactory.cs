using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public interface IConnectionFactory
    {
        IConnection EstablishNewConnection(ServiceEndPoint serviceEndpoint, ILog log);
    }
}
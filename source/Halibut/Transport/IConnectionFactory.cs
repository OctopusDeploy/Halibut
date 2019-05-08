using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public interface IConnectionFactory
    {
        SecureConnection EstablishNewConnection(ServiceEndPoint serviceEndpoint, ILog log);
    }
}
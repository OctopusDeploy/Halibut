using Halibut.Diagnostics;

namespace Halibut.Transport
{
    interface IConnectionFactory
    {
        IConnection EstablishNewConnection(ServiceEndPoint serviceEndpoint, ILog log);
    }
}
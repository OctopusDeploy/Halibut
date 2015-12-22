using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public interface IConnection : IPooledResource
    {
        MessageExchangeProtocol Protocol { get; } 
    }
}
using System;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    interface IConnection : IPooledResource
    {
        MessageExchangeProtocol Protocol { get; }
    }
}
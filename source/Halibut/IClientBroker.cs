using System;

namespace Halibut
{
    public interface IClientBroker
    {
        TService CreateClient<TService>(ServiceEndPoint endPoint);
    }
}
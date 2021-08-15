using System;
using System.Collections.Generic;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IServiceFactory
    {
        IServiceLease CreateService(string serviceName);

        IReadOnlyList<Type> RegisteredServiceTypes { get; }
    }
}
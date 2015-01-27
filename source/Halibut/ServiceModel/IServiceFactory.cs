using System;

namespace Halibut.ServiceModel
{
    public interface IServiceFactory
    {
        IServiceLease CreateService(string serviceName);
    }
}
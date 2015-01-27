using System;

namespace Halibut.ServiceModel
{
    public class NullServiceFactory : IServiceFactory
    {
        public IServiceLease CreateService(string serviceName)
        {
            throw new InvalidOperationException("An attempt was made to call the service '" + serviceName + "' on this machine, but this server has been configured to be a client only.");
        }
    }
}
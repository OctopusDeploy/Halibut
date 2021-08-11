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

    public static class ServiceFactoryExtensionMethods
    {
        public static ExchangeProtocolBuilder ExchangeProtocolBuilder(this IServiceFactory factory)
        {
            return (stream, log) => new MessageExchangeProtocol(new MessageExchangeStream(stream, factory.RegisteredServiceTypes, log), log);
        }
    }
}
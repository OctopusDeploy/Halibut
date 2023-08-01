using System;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Halibut.Tests.Support
{
    public class PendingRequestQueueFactoryBuilder
    {
        public ILogFactory LogFactory { get; }

        ForceClientProxyType? forceClientProxyType;
        Func<ILogFactory, IPendingRequestQueueFactory, IPendingRequestQueueFactory>? createDecorator;

        public PendingRequestQueueFactoryBuilder(ILogFactory logFactory)
        {
            this.LogFactory = logFactory;
        }

        public PendingRequestQueueFactoryBuilder WithForceClientProxyType(ForceClientProxyType? forceClientProxyType)
        {
            this.forceClientProxyType = forceClientProxyType;
            return this;
        }

        public PendingRequestQueueFactoryBuilder WithDecorator(Func<ILogFactory, IPendingRequestQueueFactory, IPendingRequestQueueFactory> createDecorator)
        {
            this.createDecorator = createDecorator;
            return this;
        }

        public IPendingRequestQueueFactory Build()
        {
            var factory =  CreateBaselinePendingRequestQueueFactory();
            if (createDecorator is not null)
            {
                factory = createDecorator(LogFactory, factory);
            }

            return factory;
        }

        IPendingRequestQueueFactory CreateBaselinePendingRequestQueueFactory()
        {
            if (forceClientProxyType == ForceClientProxyType.AsyncClient)
            {
                return new PendingRequestQueueFactoryAsync(LogFactory);
            }

            return new DefaultPendingRequestQueueFactory(LogFactory);
        }
    }
}
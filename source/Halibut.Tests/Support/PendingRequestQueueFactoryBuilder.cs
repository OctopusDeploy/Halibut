using System;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Halibut.Tests.Support
{
    public class PendingRequestQueueFactoryBuilder
    {
        readonly ILogFactory logFactory;
        Func<ILogFactory, IPendingRequestQueueFactory, IPendingRequestQueueFactory>? createDecorator;

        public PendingRequestQueueFactoryBuilder(ILogFactory logFactory)
        {
            this.logFactory = logFactory;
        }
        
        public PendingRequestQueueFactoryBuilder WithDecorator(Func<ILogFactory, IPendingRequestQueueFactory, IPendingRequestQueueFactory> createDecorator)
        {
            this.createDecorator = createDecorator;
            return this;
        }

        public IPendingRequestQueueFactory Build()
        {
            IPendingRequestQueueFactory factory = new PendingRequestQueueFactoryAsync(new HalibutTimeoutsAndLimitsForTestsBuilder().Build(), logFactory);
            if (createDecorator is not null)
            {
                factory = createDecorator(logFactory, factory);
            }

            return factory;
        }
    }
}
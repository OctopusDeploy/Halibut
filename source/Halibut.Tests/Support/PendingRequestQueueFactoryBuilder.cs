using System;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Halibut.Tests.Support
{
    public class PendingRequestQueueFactoryBuilder
    {
        readonly ILogFactory logFactory;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        Func<ILogFactory, IPendingRequestQueueFactory, IPendingRequestQueueFactory>? createDecorator;

        public PendingRequestQueueFactoryBuilder(ILogFactory logFactory, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.logFactory = logFactory;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
        }
        
        public PendingRequestQueueFactoryBuilder WithDecorator(Func<ILogFactory, IPendingRequestQueueFactory, IPendingRequestQueueFactory> createDecorator)
        {
            this.createDecorator = createDecorator;
            return this;
        }

        public IPendingRequestQueueFactory Build()
        {
            IPendingRequestQueueFactory factory = new PendingRequestQueueFactoryAsync(halibutTimeoutsAndLimits, logFactory);
            if (createDecorator is not null)
            {
                factory = createDecorator(logFactory, factory);
            }

            return factory;
        }
    }
}
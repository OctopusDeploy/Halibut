using System;
using System.IO;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.Support.TestAttributes;

namespace Halibut.Tests.Support
{
    public class PendingRequestQueueFactoryBuilder
    {
        readonly ILogFactory logFactory;

        SyncOrAsync? syncOrAsync;
        Func<ILogFactory, IPendingRequestQueueFactory, IPendingRequestQueueFactory>? createDecorator;

        public PendingRequestQueueFactoryBuilder(ILogFactory logFactory)
        {
            this.logFactory = logFactory;
        }

        public PendingRequestQueueFactoryBuilder WithSyncOrAsync(SyncOrAsync syncOrAsync)
        {
            this.syncOrAsync = syncOrAsync;
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
                factory = createDecorator(logFactory, factory);
            }

            return factory;
        }

        IPendingRequestQueueFactory CreateBaselinePendingRequestQueueFactory()
        {
            switch (syncOrAsync)
            {
                case SyncOrAsync.Async:
                    return new PendingRequestQueueFactoryAsync(new HalibutTimeoutsAndLimits(), logFactory);
                case SyncOrAsync.Sync:
                    return new DefaultPendingRequestQueueFactory(logFactory);
                default:
                    throw new InvalidDataException($"Unknown {nameof(SyncOrAsync)} {syncOrAsync}");
            }
        }
    }
}
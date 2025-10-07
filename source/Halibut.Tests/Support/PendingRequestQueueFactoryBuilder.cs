using System;
using Halibut.Diagnostics;
using Halibut.Queue;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.RedisDataLossDetection;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestSetup.Redis;

namespace Halibut.Tests.Support
{
    public class PendingRequestQueueFactoryBuilder
    {
        readonly PollingQueueTestCase pollingQueueTestCase;
        readonly ILogFactory logFactory;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        Func<ILogFactory, IPendingRequestQueueFactory, IPendingRequestQueueFactory>? createDecorator;
        TimeSpan? pollingQueueWaitTimeout;

        public PendingRequestQueueFactoryBuilder(PollingQueueTestCase pollingQueueTestCase, 
            ILogFactory logFactory,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.pollingQueueTestCase = pollingQueueTestCase;
            this.logFactory = logFactory;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
        }
        
        public PendingRequestQueueFactoryBuilder WithDecorator(Func<ILogFactory, IPendingRequestQueueFactory, IPendingRequestQueueFactory> createDecorator)
        {
            this.createDecorator = createDecorator;
            return this;
        }

        public PendingRequestQueueFactoryBuilder WithPollingQueueWaitTimeout(TimeSpan pollingQueueWaitTimeout)
        {
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
            return this;
        }

        public IPendingRequestQueueFactory Build(QueueMessageSerializer messageSerializer)
        {
            

            var factory = pollingQueueTestCase == PollingQueueTestCase.InMemory
                ? new PendingRequestQueueFactoryAsync(halibutTimeoutsAndLimits, logFactory)
                : CreateRedisQueueFactory(messageSerializer); 
            
            if (createDecorator is not null)
            {
                factory = createDecorator(logFactory, factory);
            }

            return factory;
        }

        IPendingRequestQueueFactory CreateRedisQueueFactory(QueueMessageSerializer messageSerializer)
        {
            var disposableCollection = new Halibut.Util.DisposableCollection();

            var log = logFactory.ForPrefix("RedisQueue");
            
            var redisFacade = new RedisFacade("localhost:" + RedisTestHost.Port(), (Guid.NewGuid()).ToString(), log);
            disposableCollection.AddAsyncDisposable(redisFacade);
            
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();

            
            var watchForRedisLosingAllItsData = new WatchForRedisLosingAllItsData(redisFacade, log);
            var queueFactory = new RedisPendingRequestQueueFactory(messageSerializer, 
                dataStreamStore,
                watchForRedisLosingAllItsData,
                redisTransport,
                halibutTimeoutsAndLimits,
                logFactory);
            
            return queueFactory;
        }
    }
}

#if NET8_0_OR_GREATER
using System;
using Halibut.Logging;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.Cancellation;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Tests.Queue;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestSetup.Redis;
using Halibut.Util;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Builders
{
    public class RedisPendingRequestQueueBuilder : IPendingRequestQueueBuilder
    {
        ILog? log;
        string? endpoint;
        TimeSpan? pollingQueueWaitTimeout;
        TimeSpan? defaultDelayBeforeSubscribingToRequestCancellation;

        public IPendingRequestQueueBuilder WithEndpoint(string endpoint)
        {
            this.endpoint = endpoint;
            return this;
        }

        public IPendingRequestQueueBuilder WithLog(ILog log)
        {
            this.log = log;
            return this;
        }

        public IPendingRequestQueueBuilder WithPollingQueueWaitTimeout(TimeSpan? pollingQueueWaitTimeout)
        {
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
            return this;
        }

        public IPendingRequestQueueBuilder WithDelayBeforeCheckingForCancellation(TimeSpan defaultDelayBeforeSubscribingToRequestCancellation)
        {
            this.defaultDelayBeforeSubscribingToRequestCancellation = defaultDelayBeforeSubscribingToRequestCancellation;
            return this;
        }

        public QueueHolder Build()
        {
            var endpoint = new Uri(this.endpoint ?? "poll://endpoint001");
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            var log = this.log ?? new TestContextLogCreator("Queue", LogLevel.Trace).CreateNewForPrefix("");

            if (this.pollingQueueWaitTimeout != null)
            {
                halibutTimeoutsAndLimits.PollingQueueWaitTimeout = pollingQueueWaitTimeout.Value;
            }

            var disposableCollection = new DisposableCollection();

            var redisFacade = new RedisFacade("localhost:" + RedisTestHost.Port(), (Guid.NewGuid()).ToString(), log);
            disposableCollection.AddAsyncDisposable(redisFacade);
            
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageSerialiserAndDataStreamStorage(messageSerializer, dataStreamStore);

            var queue = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), log, redisTransport, messageReaderWriter, halibutTimeoutsAndLimits);
            if (defaultDelayBeforeSubscribingToRequestCancellation != null)
            {
                queue.DelayBeforeSubscribingToRequestCancellation = new DelayBeforeSubscribingToRequestCancellation(defaultDelayBeforeSubscribingToRequestCancellation.Value);
            }
            queue.WaitUntilQueueIsSubscribedToReceiveMessages().GetAwaiter().GetResult();
            
            return new QueueHolder(queue, disposableCollection);
        }
    }
}
#endif
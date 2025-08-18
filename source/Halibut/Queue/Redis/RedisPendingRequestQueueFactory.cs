
#if NET8_0_OR_GREATER
using System;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.RedisDataLoseDetection;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.ServiceModel;

namespace Halibut.Queue.Redis
{
    public class RedisPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        readonly QueueMessageSerializer queueMessageSerializer;
        readonly IStoreDataStreamsForDistributedQueues dataStreamStorage;
        readonly HalibutRedisTransport halibutRedisTransport;
        readonly ILogFactory logFactory;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly IWatchForRedisLosingAllItsData watchForRedisLosingAllItsData;

        public RedisPendingRequestQueueFactory(
            QueueMessageSerializer queueMessageSerializer,
            IStoreDataStreamsForDistributedQueues dataStreamStorage,
            IWatchForRedisLosingAllItsData watchForRedisLosingAllItsData,
            HalibutRedisTransport halibutRedisTransport,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, 
            ILogFactory logFactory)
        {
            this.queueMessageSerializer = queueMessageSerializer;
            this.dataStreamStorage = dataStreamStorage;
            this.halibutRedisTransport = halibutRedisTransport;
            this.logFactory = logFactory;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.watchForRedisLosingAllItsData = watchForRedisLosingAllItsData;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            return new RedisPendingRequestQueue(endpoint,
                watchForRedisLosingAllItsData,
                logFactory.ForEndpoint(endpoint),
                halibutRedisTransport,
                new MessageSerialiserAndDataStreamStorage(queueMessageSerializer, dataStreamStorage),
                halibutTimeoutsAndLimits);
        }

    }
}

#endif
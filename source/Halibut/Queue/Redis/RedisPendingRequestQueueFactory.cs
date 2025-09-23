
#if NET8_0_OR_GREATER
using System;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.RedisDataLossDetection;
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
        readonly IMessageSerialiserAndDataStreamStorageExceptionObserver exceptionObserver;
        readonly Func<IPendingRequestQueue, IPendingRequestQueue>? queueDecorator;

        public RedisPendingRequestQueueFactory(
            QueueMessageSerializer queueMessageSerializer,
            IStoreDataStreamsForDistributedQueues dataStreamStorage,
            IWatchForRedisLosingAllItsData watchForRedisLosingAllItsData,
            HalibutRedisTransport halibutRedisTransport,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits,
            ILogFactory logFactory,
            IMessageSerialiserAndDataStreamStorageExceptionObserver? exceptionObserver = null,
            Func<IPendingRequestQueue, IPendingRequestQueue>? queueDecorator = null)
        {
            this.queueMessageSerializer = queueMessageSerializer;
            this.dataStreamStorage = dataStreamStorage;
            this.halibutRedisTransport = halibutRedisTransport;
            this.logFactory = logFactory;
            this.queueDecorator = queueDecorator;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.watchForRedisLosingAllItsData = watchForRedisLosingAllItsData;
            this.exceptionObserver = exceptionObserver ?? NoOpMessageSerialiserAndDataStreamStorageExceptionObserver.Instance;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            var baseStorage = new MessageSerialiserAndDataStreamStorage(queueMessageSerializer, dataStreamStorage);
            var storageWithObserver = new MessageSerialiserAndDataStreamStorageWithExceptionObserver(baseStorage, exceptionObserver);
            
            var queue =  new RedisPendingRequestQueue(endpoint,
                watchForRedisLosingAllItsData,
                logFactory.ForEndpoint(endpoint),
                halibutRedisTransport,
                storageWithObserver,
                halibutTimeoutsAndLimits);

            if (queueDecorator != null) return queueDecorator(queue);
            
            return queue;
        }

    }
}

#endif
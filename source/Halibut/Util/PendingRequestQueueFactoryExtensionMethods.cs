using System;
using Halibut.Queue.Redis;
using Halibut.ServiceModel;

namespace Halibut.Util
{
    public static class PendingRequestQueueFactoryExtensionMethods
    {
#if NET8_0_OR_GREATER
        internal static IPendingRequestQueueFactory ModifyRedisQueue(
            this IPendingRequestQueueFactory pendingRequestQueueFactory, 
            Func<RedisPendingRequestQueue, RedisPendingRequestQueue> redisQueueModifier)
        {
            return new RedisQueueModifyingPendingRequestQueueFactory(pendingRequestQueueFactory, redisQueueModifier);
        }
#endif
        
        public static IPendingRequestQueueFactory CaptureCreatedQueues(this IPendingRequestQueueFactory pendingRequestQueueFactory, Action<IPendingRequestQueue> onCreated)
        {
            return new CaptureQueueCreatedPendingRequestQueueFactory(pendingRequestQueueFactory, onCreated);
        }
    }

    public class CaptureQueueCreatedPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        IPendingRequestQueueFactory inner;
        Action<IPendingRequestQueue> onCreated;

        public CaptureQueueCreatedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, Action<IPendingRequestQueue> onCreated)
        {
            this.inner = inner;
            this.onCreated = onCreated;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            var queue = inner.CreateQueue(endpoint);
            onCreated(queue);
            return queue;
        }
    }
#if NET8_0_OR_GREATER
    public class RedisQueueModifyingPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        IPendingRequestQueueFactory inner;
        internal Func<RedisPendingRequestQueue, RedisPendingRequestQueue> redisQueueModifier;

        internal RedisQueueModifyingPendingRequestQueueFactory(IPendingRequestQueueFactory inner, Func<RedisPendingRequestQueue, RedisPendingRequestQueue> redisQueueModifier)
        {
            this.inner = inner;
            this.redisQueueModifier = redisQueueModifier;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            var queue = inner.CreateQueue(endpoint);

            if (queue is RedisPendingRequestQueue redisPendingRequestQueue) return redisQueueModifier(redisPendingRequestQueue);
            return queue;
        }
    }
#endif
}
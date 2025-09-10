
#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using Halibut.Queue.Redis;
using Halibut.ServiceModel;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class TestRedisPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        RedisPendingRequestQueueFactory redisPendingRequestQueueFactory;
        List<Action<RedisPendingRequestQueue>> pendingRequestQueueCallBacks = new();

        internal TestRedisPendingRequestQueueFactory WithCallback(Action<RedisPendingRequestQueue> callback)
        {
            pendingRequestQueueCallBacks.Add(callback);
            return this;
        } 
        public TestRedisPendingRequestQueueFactory(RedisPendingRequestQueueFactory redisPendingRequestQueueFactory)
        {
            this.redisPendingRequestQueueFactory = redisPendingRequestQueueFactory;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            var queue = (RedisPendingRequestQueue) redisPendingRequestQueueFactory.CreateQueue(endpoint);
            foreach (var pendingRequestQueueCallBack in pendingRequestQueueCallBacks)
            {
                pendingRequestQueueCallBack(queue);
            }
            return queue;
        }
    }

    public static class RedisPendingRequestQueueFactoryExtensionMethods
    {
        public static TestRedisPendingRequestQueueFactory WithWaitForReceiverToBeReady(this RedisPendingRequestQueueFactory redisPendingRequestQueueFactory)
        {
            return redisPendingRequestQueueFactory
                .WithQueueCreationCallBack(queue => queue.WaitUntilQueueIsSubscribedToReceiveMessages().GetAwaiter().GetResult());
        }

        internal static TestRedisPendingRequestQueueFactory WithQueueCreationCallBack(this RedisPendingRequestQueueFactory redisPendingRequestQueueFactory, Action<RedisPendingRequestQueue> queueCreatedCallback)
        {
            return new TestRedisPendingRequestQueueFactory(redisPendingRequestQueueFactory)
                .WithCallback(queueCreatedCallback);
        }
        
        internal static TestRedisPendingRequestQueueFactory WithQueueCreationCallBack(this TestRedisPendingRequestQueueFactory redisPendingRequestQueueFactory, Action<RedisPendingRequestQueue> queueCreatedCallback)
        {
            return redisPendingRequestQueueFactory.WithCallback(queueCreatedCallback);
        }
    }
}
#endif
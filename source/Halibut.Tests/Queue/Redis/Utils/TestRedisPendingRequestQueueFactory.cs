
#if NET8_0_OR_GREATER
using System;
using Halibut.Queue.Redis;
using Halibut.ServiceModel;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class TestRedisPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        RedisPendingRequestQueueFactory redisPendingRequestQueueFactory;

        public TestRedisPendingRequestQueueFactory(RedisPendingRequestQueueFactory redisPendingRequestQueueFactory)
        {
            this.redisPendingRequestQueueFactory = redisPendingRequestQueueFactory;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            var queue = (RedisPendingRequestQueue) redisPendingRequestQueueFactory.CreateQueue(endpoint);
#pragma warning disable VSTHRD002
            queue.WaitUntilQueueIsSubscribedToReceiveMessages().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            return queue;
        }
    }

    public static class RedisPendingRequestQueueFactoryExtensionMethods
    {
        public static IPendingRequestQueueFactory WithWaitForReceiverToBeReady(this RedisPendingRequestQueueFactory redisPendingRequestQueueFactory)
        {
            return new TestRedisPendingRequestQueueFactory(redisPendingRequestQueueFactory);
        }
    }
}
#endif
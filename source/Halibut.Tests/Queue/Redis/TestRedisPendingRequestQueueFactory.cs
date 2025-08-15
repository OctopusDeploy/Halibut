// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#if NET8_0_OR_GREATER
using System;
using Halibut.Queue.Redis;
using Halibut.ServiceModel;

namespace Halibut.Tests.Queue.Redis
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
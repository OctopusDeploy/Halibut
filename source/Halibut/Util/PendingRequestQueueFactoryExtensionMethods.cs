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

using System;
using Halibut.Queue.Redis;
using Halibut.ServiceModel;

namespace Halibut.Util
{
    public static class PendingRequestQueueFactoryExtensionMethods
    {
        internal static IPendingRequestQueueFactory ModifyRedisQueue(
            this IPendingRequestQueueFactory pendingRequestQueueFactory, 
            Func<RedisPendingRequestQueue, RedisPendingRequestQueue> redisQueueModifier)
        {
            return new RedisQueueModifyingPendingRequestQueueFactory(pendingRequestQueueFactory, redisQueueModifier);
        }
    }

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
}
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
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.QueuedDataStreams;
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
                new MessageReaderWriter(queueMessageSerializer, dataStreamStorage),
                halibutTimeoutsAndLimits);
        }

    }
}
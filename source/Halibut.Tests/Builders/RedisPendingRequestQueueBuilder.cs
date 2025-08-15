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
using Halibut.Logging;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis;
using Halibut.Tests.Queue;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support.Logging;
using Halibut.Util;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Builders
{
    public class RedisPendingRequestQueueBuilder : IPendingRequestQueueBuilder
    {
        
        const int redisPort = 6379;
        
        ILog? log;
        string? endpoint;
        TimeSpan? pollingQueueWaitTimeout;

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

            var redisFacade = new RedisFacade("localhost:" + redisPort, (Guid.NewGuid()).ToString(), log);
            disposableCollection.AddAsyncDisposable(redisFacade);
            
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queue = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, halibutTimeoutsAndLimits);
            
#pragma warning disable VSTHRD002
            queue.WaitUntilQueueIsSubscribedToReceiveMessages().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            
            return new QueueHolder(queue, disposableCollection);
        }
    }
}
#endif
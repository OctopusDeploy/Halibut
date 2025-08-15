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
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Logging;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis;
using Halibut.Tests.Builders;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support.Logging;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Queue.Redis
{
    [Ignore("REDISTODO")]
    public class RedisQueuePerformanceTest : BaseTest
    {
        const int redisPort = 6379;
        private static RedisFacade CreateRedisFacade(int? port = redisPort, Guid? guid = null) => new("localhost:" + port, (guid ?? Guid.NewGuid()).ToString(), new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));

        
        //[Test]
        public async Task When100kTentaclesAreSubscribed_TheQueueStillWorks()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);

            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimits();

            await using var disposableCollection = new DisposableCollection();
            for (int i = 0; i < 300000; i++)
            {
                disposableCollection.Add(new RedisPendingRequestQueue(new Uri("poll://" + Guid.NewGuid()), new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, halibutTimeoutsAndLimits));
                if (i % 10000 == 0)
                {
                    Logger.Information("Up to: {i}", i);
                }
            }

            this.Logger.Fatal("Waiting");
            await Task.Delay(30000);
            this.Logger.Fatal("Done");

            for (int i = 0; i < 10; i++)
            {
                var request = new RequestMessageBuilder(endpoint.ToString()).Build();
                await using var sut = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, halibutTimeoutsAndLimits);

                var resultTask = sut.DequeueAsync(CancellationToken);

                await Task.Delay(100);

                var sw = Stopwatch.StartNew();

                var task = sut.QueueAndWaitAsync(request, CancellationToken);

                var result = await resultTask;
                // Act

                // Assert
                result.Should().NotBeNull();
                result!.RequestMessage.Id.Should().Be(request.Id);
                result.RequestMessage.MethodName.Should().Be(request.MethodName);
                result.RequestMessage.ServiceName.Should().Be(request.ServiceName);
                Logger.Information("It took {F}", sw.Elapsed.TotalSeconds.ToString("0.00"));
            }
        }
    }
}
#endif
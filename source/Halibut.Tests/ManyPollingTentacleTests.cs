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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet.Models;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Logging;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;
using DisposableCollection = Halibut.Util.DisposableCollection;

namespace Halibut.Tests
{
    [NonParallelizable]
    public class ManyPollingTentacleTests : BaseTest
    {
        [Test]
        [AllQueuesTestCases]
        [NonParallelizable]
        public async Task ManyRequestToPollingTentacles_Works_AndDoesNotUseTooManyResources(PendingRequestQueueTestCase queueTestCase)
        {
            var logFactory = new CachingLogFactory(new TestContextLogCreator("", LogLevel.Trace));
            var services = GetDelegateServiceFactory();
            await using var disposables = new DisposableCollection();
            var isRedis = queueTestCase.ToString().ToLower().Contains("redis");
            var log = new TestContextLogCreator("Redis", LogLevel.Fatal);
            await using var redisFacade = new RedisFacade("localhost:6379", Guid.NewGuid().ToString(), log.CreateNewForPrefix(""));
            await using (var octopus = new HalibutRuntimeBuilder()
                             .WithServerCertificate(Certificates.Octopus)
                             .WithPendingRequestQueueFactory(msgSer =>
                             {
                                 if (isRedis)
                                 {
                                     var watchForRedisLosingAllItsData = new WatchForRedisLosingAllItsData(redisFacade, log.CreateNewForPrefix("watcher"));
                                     disposables.AddAsyncDisposable(watchForRedisLosingAllItsData);
                                     
                                     return new RedisPendingRequestQueueFactory(msgSer,
                                         new InMemoryStoreDataStreamsForDistributedQueues(),
                                         watchForRedisLosingAllItsData,
                                         new HalibutRedisTransport(redisFacade),
                                         new HalibutTimeoutsAndLimitsForTestsBuilder().Build(),
                                         logFactory);
                                 }

                                 return new PendingRequestQueueFactoryAsync(new HalibutTimeoutsAndLimitsForTestsBuilder().Build(),
                                     logFactory);
                             })
                             .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                             .Build())
            {
                var listenPort = octopus.Listen();
                octopus.Trust(Certificates.TentacleListening.Thumbprint);
                
                var _ = Task.Run(async () =>
                {
                    while (!CancellationToken.IsCancellationRequested)
                    {
                        GC.Collect();
                        Logger.Information("Total subscribers: {TotalSubs}", redisFacade.TotalSubscribers);
                        await Task.Delay(10000);
                    }
                });

                var serviceEndpoint = new ServiceEndPoint(new Uri("https://localhost:" + listenPort), Certificates.Octopus.Thumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
                
                
                var pollEndpoints = Enumerable.Range(0, 100).Select(i => new Uri("poll://" + i + "Bob")).ToArray();
                
                foreach (var pollEndpoint in pollEndpoints)
                {
                    var tentacleListening = new HalibutRuntimeBuilder()
                        .WithServerCertificate(Certificates.TentacleListening)
                        .WithServiceFactory(services)
                        .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                        .Build();
                    tentacleListening.Poll(pollEndpoint, serviceEndpoint, CancellationToken);
                }

                var clients = pollEndpoints.Select(pollEndpoint => 
                    octopus.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(new ServiceEndPoint(pollEndpoint, Certificates.Octopus.Thumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build())))
                    .ToList();

                var tasks = new List<Task>();

                int concurrency = 20;
                int limit = 20;
                int total = concurrency * Math.Min(clients.Count, limit);
                int callsMade = 0;

                var totalSw = Stopwatch.StartNew();
                for (int i = 0; i < concurrency; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        var shuffle = clients.ToArray();
                        Random.Shared.Shuffle(shuffle);
                        shuffle = shuffle.Take(limit).ToArray();
                        foreach (var client in shuffle)
                        {
                            await client.SayHelloAsync("World");
                            var v = Interlocked.Increment(ref callsMade);
                            if (v % 5000 == 0)
                            {
                                var timePerCall = totalSw.ElapsedMilliseconds / v;
                                Logger.Information("Done: {CallsMade} / {Total} avg: {A}", v, total, timePerCall);
                            }
                            
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                
                totalSw.Stop();
                
                Logger.Information("Time was {T}", totalSw.ElapsedMilliseconds);
                
                callsMade.Should().Be(total);

                if(isRedis)
                {
                    redisFacade.TotalSubscribers.Should().Be(pollEndpoints.Length);
                }
                
                foreach (var task in tasks)
                {
                    await task;
                }
            }
        }
        
        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService, IAsyncEchoService>(() => new AsyncEchoServiceWithDelay());
            return services;
        }
    }
    
    public class AsyncEchoServiceWithDelay : IAsyncEchoService
    {
        
        public async Task<int> LongRunningOperationAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(10000, cancellationToken);
            return 16;
        }

        public async Task<string> SayHelloAsync(string name, CancellationToken cancellationToken)
        {
            
            await Task.Delay(0);
            return name + "...";
        }

        public async Task<bool> CrashAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            throw new DivideByZeroException();
        }

        public async Task<int> CountBytesAsync(DataStream dataStream, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            throw new Exception();
        }
    }
}
#endif
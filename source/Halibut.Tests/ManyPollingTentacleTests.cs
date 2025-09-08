
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
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.RedisDataLossDetection;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.ServiceModel;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
using NUnit.Framework;
using DisposableCollection = Halibut.Util.DisposableCollection;

namespace Halibut.Tests
{
    [NonParallelizable]
    [RedisTest]
    public class ManyPollingTentacleTests : BaseTest
    {
        /// <summary>
        /// Fuzz test, to check under load the queue still works.
        /// 
        /// </summary>
        /// <param name="queueTestCase"></param>
        [Test]
        [AllQueuesTestCases]
        [NonParallelizable]
        public async Task WhenMakingManyConcurrentRequestsToManyServices_AllRequestsCompleteSuccessfully_And(PendingRequestQueueTestCase queueTestCase)
        {
            var numberOfPollingServices = 100;
            int concurrency = 20;
            int numberOfCallsToMake = Math.Min(numberOfPollingServices, 20);
            
            var logFactory = new CachingLogFactory(new TestContextLogCreator("", LogLevel.Trace));
            var services = GetDelegateServiceFactory();
            await using var disposables = new DisposableCollection();
            var isRedis = queueTestCase.Name == PendingRequestQueueTestCase.RedisTestCaseName;
            var log = new TestContextLogCreator("Redis", LogLevel.Fatal);
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
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

                var watchSubscriberCountCts = new CancelOnDisposeCancellationToken(CancellationToken);
                watchSubscriberCountCts.AwaitTasksBeforeCTSDispose(Task.Run(async () =>
                {
                    while (!watchSubscriberCountCts.Token.IsCancellationRequested)
                    {
                        Logger.Information("Total subscribers: {TotalSubs}", redisFacade.TotalSubscribers);
                        await Task.Delay(1000);
                    }
                }));

                var serviceEndpoint = new ServiceEndPoint(new Uri("https://localhost:" + listenPort), Certificates.Octopus.Thumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
                
                var pollEndpoints = Enumerable.Range(0, numberOfPollingServices).Select(i => new Uri("poll://" + i + "Bob")).ToArray();
                
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
                
                int expectedTotalNumberOfCallsToBeMade = concurrency * numberOfCallsToMake;
                int actualCountOfCallsMade = 0;

                var totalSw = Stopwatch.StartNew();
                for (int i = 0; i < concurrency; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        var shuffle = clients.ToArray();
                        Random.Shared.Shuffle(shuffle);
                        shuffle = shuffle.Take(numberOfCallsToMake).ToArray();
                        foreach (var client in shuffle)
                        {
                            await client.SayHelloAsync("World");
                            var v = Interlocked.Increment(ref actualCountOfCallsMade);
                            if (v % 5000 == 0)
                            {
                                var timePerCall = totalSw.ElapsedMilliseconds / v;
                                Logger.Information("Done: {CallsMade} / {Total} avg: {A}", v, expectedTotalNumberOfCallsToBeMade, timePerCall);
                            }
                            
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                
                totalSw.Stop();
                
                Logger.Information("Time was {T}", totalSw.ElapsedMilliseconds);
                
                actualCountOfCallsMade.Should().Be(expectedTotalNumberOfCallsToBeMade);

                if(isRedis)
                {
                    redisFacade.TotalSubscribers.Should().Be(pollEndpoints.Length);
                }
                
                // Check for exceptions.
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
        
        public Task<int> LongRunningOperationAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<string> SayHelloAsync(string name, CancellationToken cancellationToken)
        {
            
            await Task.Delay(10, cancellationToken);
            return name + "...";
        }

        public Task<bool> CrashAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<int> CountBytesAsync(DataStream dataStream, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
#endif
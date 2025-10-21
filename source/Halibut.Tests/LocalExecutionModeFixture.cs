#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Logging;
using Halibut.Queue;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.RedisDataLossDetection;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.ServiceModel;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;
using DisposableCollection = Halibut.Util.DisposableCollection;

namespace Halibut.Tests
{
    public class LocalExecutionModeFixture : BaseTest
    {
        [RedisTest]
        [Test]
        public async Task SimpleLocalExecutionExample()
        {
            var services = GetDelegateServiceFactory();
            var timeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            timeoutsAndLimits = new HalibutTimeoutsAndLimits();

            // Use a shared queue factory so client and worker share the same queue
            var queueFactory = new PendingRequestQueueFactoryAsync(timeoutsAndLimits, new LogFactory());
            
            var logFactory = new CachingLogFactory(new TestContextLogCreator("", LogLevel.Trace));
            
            var log = new TestContextLogCreator("Redis", LogLevel.Fatal);
            
            var preSharedGuid = Guid.NewGuid();
            
            await using var disposables = new DisposableCollection();

            await using var client = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Octopus)
                .WithPendingRequestQueueFactory(RedisFactory())
                .WithHalibutTimeoutsAndLimits(timeoutsAndLimits)
                .Build();

            await using var worker = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.TentaclePolling)
                .WithServiceFactory(services)
                .WithPendingRequestQueueFactory(RedisFactory())
                .WithHalibutTimeoutsAndLimits(timeoutsAndLimits)
                .Build();

            // Start worker polling for local://test-worker
            using var workerCts = new CancellationTokenSource();
            var pollingTask = Task.Run(async () =>
            {
                //await Task.Delay(TimeSpan.FromSeconds(10));
                await worker.PollLocalAsync(new Uri("local://test-worker"), workerCts.Token);
            }, workerCts.Token);

            // Client creates proxy to local://test-worker and makes request
            var echo = client.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(
                new ServiceEndPoint("local://test-worker", null, client.TimeoutsAndLimits));

            var result = await echo.SayHelloAsync("World");
            result.Should().Be("World...");

            // Cleanup
            workerCts.Cancel();
            // try
            // {
            //     await pollingTask;
            // }
            // catch (OperationCanceledException)
            // {
            //     // Expected
            // }

            Func<QueueMessageSerializer, IPendingRequestQueueFactory> RedisFactory()
            {
                return msgSer =>
                {
                    var redisFacade = RedisFacadeBuilder.CreateRedisFacade(prefix: preSharedGuid);
                    disposables.AddAsyncDisposable(redisFacade);
                    var watchForRedisLosingAllItsData = new WatchForRedisLosingAllItsData(redisFacade, log.CreateNewForPrefix("watcher"));
                    disposables.AddAsyncDisposable(watchForRedisLosingAllItsData);
                                     
                    return new RedisPendingRequestQueueFactory(msgSer,
                        new InMemoryStoreDataStreamsForDistributedQueues(),
                        watchForRedisLosingAllItsData,
                        new HalibutRedisTransport(redisFacade),
                        new HalibutTimeoutsAndLimitsForTestsBuilder().Build(),
                        logFactory);
                };
            }
        }

        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());
            return services;
        }
    }
}
#endif

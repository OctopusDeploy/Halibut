#if NET8_0_OR_GREATER
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Queue.Redis;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestSetup.Redis;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Queue.Redis
{
    [RedisTest]
    public class WatchForRedisLosingAllItsDataFixture : BaseTest
    {
        [Test]
        public async Task WhenTheConnectionToRedisCanNotBeCreated_WhenAskingForALostDataCancellationToken_ATimeoutOccurs()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisTestHost.Port(), Logger).Build();
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder.ListeningPort, null);
            await using var watcher = new WatchForRedisLosingAllItsData(redisFacade, HalibutLog, watchInterval:TimeSpan.FromSeconds(1));


            await AssertException.Throws<TaskCanceledException>(watcher.GetTokenForDataLoseDetection(TimeSpan.FromSeconds(1), CancellationToken));
        }
        
        [Test]
        public async Task WhenTheConnectionToRedisIsInitiallyDown_WhenAskingForALostDataCancellationToken_AndTheConnectionToRedisReturns_TheCTIsReturned()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisTestHost.Port(), Logger).Build();
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder.ListeningPort, null);
            await using var watcher = new WatchForRedisLosingAllItsData(redisFacade, HalibutLog, watchInterval:TimeSpan.FromSeconds(1));

            var _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                portForwarder.ReturnToNormalMode();

            });
            
            await watcher.GetTokenForDataLoseDetection(TimeSpan.FromSeconds(20), CancellationToken);
        }
        
        [Test]
        public async Task WatchForARealRedisLosingAllOfItsData_TimesOutWhenWaitingForCTWhenNoConnectionToRedisCanBeEstablished()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisTestHost.Port(), Logger).Build();
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder.ListeningPort, null);
            await using var watcher = new WatchForRedisLosingAllItsData(redisFacade, HalibutLog, watchInterval:TimeSpan.FromSeconds(1));


            await AssertException.Throws<TaskCanceledException>(watcher.GetTokenForDataLoseDetection(TimeSpan.FromSeconds(1), CancellationToken));
        }

        [Test]
        public async Task WhenRedisRunsForLongerThanTheKeyTTL_NoDataLoseShouldBeDetected()
        {
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            await using var watcher = new WatchForRedisLosingAllItsData(redisFacade, HalibutLog, watchInterval:TimeSpan.FromMilliseconds(100), keyTTL: TimeSpan.FromSeconds(2));
            var watcherCt = await watcher.GetTokenForDataLoseDetection(TimeSpan.FromSeconds(20), CancellationToken);

            await Task.Delay(TimeSpan.FromSeconds(4));
            watcherCt.IsCancellationRequested.Should().BeFalse();
        }

        [Test]
        public async Task WhenRedisLosesAllOfIts_TheWatcherShouldDetectTheDataLose()
        {
            Logger.Information("Starting WatchForARealRedisLosingAllOfItsData_E2E_Test");
            
            // Arrange - Create Redis container using the builder
            Logger.Information("Creating Redis container");
            await using var container = new RedisContainerBuilder()
                .Build();
            
            Logger.Information("Starting Redis container");
            await container.StartAsync();
            Logger.Information("Redis container started successfully with connection string: {ConnectionString}", container.ConnectionString);

            // Create RedisFacade connected to the containerized Redis
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(container.RedisPort, null);
            
            await using var watcher = new WatchForRedisLosingAllItsData(redisFacade, HalibutLog, watchInterval:TimeSpan.FromSeconds(1));
            
            Logger.Information("Getting initial cancellation token for data loss detection (20 second timeout)");
            var watcherCT = await watcher.GetTokenForDataLoseDetection(TimeSpan.FromSeconds(20), CancellationToken);
            Logger.Information("Initial cancellation token obtained, IsCancellationRequested: {IsCancellationRequested}", watcherCT.IsCancellationRequested);

            watcherCT.IsCancellationRequested.Should().BeFalse();
            
            // Act
            Logger.Information("Stopping Redis container to simulate data loss");
            await container.StopAsync();
            Logger.Information("Redis container stopped");
            
            Logger.Information("Starting Redis container again (fresh instance, data lost)");
            await container.StartAsync();
            Logger.Information("Redis container restarted");
            
            // Assert
            Logger.Information("Waiting up to 10 seconds for data loss detection to trigger cancellation token");
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(10), watcherCT));
            
            watcherCT.IsCancellationRequested.Should().BeTrue("Should have detected the data loss");

            Logger.Information("Getting new cancellation token to verify recovery");
            var nextToken = await watcher.GetTokenForDataLoseDetection(TimeSpan.FromSeconds(20), CancellationToken);
            
            nextToken.IsCancellationRequested.Should().BeFalse("The new token should have no data loss");
        }
    }
}
#endif
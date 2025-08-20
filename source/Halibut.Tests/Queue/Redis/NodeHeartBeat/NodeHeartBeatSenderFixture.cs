#if NET8_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.NodeHeartBeat;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Tests.Builders;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestSetup.Redis;
using Nito.AsyncEx;
using NUnit.Framework;

namespace Halibut.Tests.Queue.Redis.NodeHeartBeat
{
    [RedisTest]
    public class NodeHeartBeatSenderFixture : BaseTest
    {
        [Test]
        public async Task WhenCreated_ShouldStartSendingHeartbeats()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            
            var anyHeartBeatReceived = new AsyncManualResetEvent(false);
            
            // Subscribe to heartbeats before creating the sender
            await using var subscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.RequestProcessorNode, async () =>
                {
                    await Task.CompletedTask;
                    anyHeartBeatReceived.Set();
                }, CancellationToken);

            // Act
            await using var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.RequestProcessorNode, defaultDelayBetweenPulses: TimeSpan.FromSeconds(1));
            
            // Wait for a heart beat.
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(20), CancellationToken), anyHeartBeatReceived.WaitAsync());

            // Assert
            anyHeartBeatReceived.IsSet.Should().BeTrue("Should have received at least one heartbeat");
        }

        
        [Test]
        public async Task WhenHeartBeatsAreBeingSent_AndTheConnectionToRedisIsBrieflyDown_HeatBeatsShouldBeSentAgain()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            await using var unstableRedisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder, guid);
            await using var stableRedisFacade = RedisFacadeBuilder.CreateRedisFacade(port: RedisTestHost.Port(), prefix: guid);
            
            var redisTransport = new HalibutRedisTransport(unstableRedisFacade);
            
            var heartbeatsReceived = new ConcurrentBag<DateTimeOffset>();
            var heartBeatReceivedEvent = new AsyncManualResetEvent(false);
            
            // Subscribe with stable connection to monitor heartbeats
            await using var subscription = await new HalibutRedisTransport(stableRedisFacade)
                .SubscribeToNodeHeartBeatChannel(
                    endpoint, requestActivityId, HalibutQueueNodeSendingPulses.RequestProcessorNode, async () =>
                    {
                        await Task.CompletedTask;
                        heartBeatReceivedEvent.Set();
                        heartbeatsReceived.Add(DateTimeOffset.Now);
                    }, CancellationToken);

            // Act
            await using var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.RequestProcessorNode, TimeSpan.FromSeconds(1));
            
            // Wait for initial heartbeat
            await heartBeatReceivedEvent.WaitAsync(CancellationToken);
            
            // Interrupt connection
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            
            // Outage is 10s
            await Task.Delay(TimeSpan.FromSeconds(4), CancellationToken);
            heartBeatReceivedEvent.Reset();
            
            // Restore connection
            portForwarder.ReturnToNormalMode();

            // Assert
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(10)), heartBeatReceivedEvent.WaitAsync(CancellationToken));
            heartBeatReceivedEvent.IsSet.Should().BeTrue("Heart beats should be sent again after the interruption.");
        }

        [Test]
        public async Task WhenDisposed_ShouldStopSendingHeartbeats()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            
            var heartbeatsReceived = new ConcurrentBag<DateTimeOffset>();
            var anyHeartBeatReceived = new AsyncManualResetEvent(false);
            
            await using var subscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.RequestProcessorNode, async () =>
                {
                    await Task.CompletedTask;
                    anyHeartBeatReceived.Set();
                    heartbeatsReceived.Add(DateTimeOffset.Now);
                }, CancellationToken);

            // Act
            var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.RequestProcessorNode, defaultDelayBetweenPulses: TimeSpan.FromSeconds(1));
            
            // Wait for some heartbeats
            await anyHeartBeatReceived.WaitAsync(CancellationToken);
            
            // Dispose the sender
            await heartBeatSender.DisposeAsync();

            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(4)), heartBeatSender.TaskSendingPulses);
            anyHeartBeatReceived.Reset();
            
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5), CancellationToken), anyHeartBeatReceived.WaitAsync());

            // Assert
            anyHeartBeatReceived.IsSet.Should().BeFalse();
            heartBeatSender.TaskSendingPulses.IsCompleted.Should().BeTrue();
        }

        [Test]
        public async Task WhenWatchingTheNodeProcessingTheRequestIsStillAlive_AndHeartbeatsStopBeingSent_ShouldReturnProcessingNodeIsLikelyDisconnected()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            await using var unstableRedisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder, guid);
            await using var stableRedisFacade = RedisFacadeBuilder.CreateRedisFacade(prefix: guid);
            
            var unstableRedisTransport = new HalibutRedisTransport(unstableRedisFacade);
            var stableRedisTransport = new HalibutRedisTransport(stableRedisFacade);
            
            var request = new RequestMessageBuilder(endpoint.ToString())
                .WithActivityId(requestActivityId)
                .Build();
            var pendingRequest = new RedisPendingRequest(request, log);
            
            // Start heartbeat sender
            await using var heartBeatSender = new NodeHeartBeatSender(
                endpoint,
                requestActivityId,
                unstableRedisTransport,
                log,
                HalibutQueueNodeSendingPulses.RequestProcessorNode,
                defaultDelayBetweenPulses: TimeSpan.FromMilliseconds(200));
            
            // Mark request as collected so watcher proceeds to monitoring phase
            await pendingRequest.RequestHasBeenCollectedAndWillBeTransferred();
            
            // Start the watcher
            var watcherTask = NodeHeartBeatWatcher.WatchThatNodeProcessingTheRequestIsStillAlive(
                endpoint, 
                request, 
                pendingRequest, 
                stableRedisTransport, 
                TimeSpan.FromSeconds(1),
                log, 
                TimeSpan.FromSeconds(5), // Short timeout for test
                CancellationToken);

            // Wait for initial heartbeats to establish baseline
            await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken);
            watcherTask.IsCompleted.Should().BeFalse();
            
            // Act - Kill the connection to stop heartbeats
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            
            // Assert
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(10)), watcherTask);
            watcherTask.IsCompleted.Should().BeTrue("Since it should have detected no heart beats have been sent for some time.");
            var result = await watcherTask;
            result.Should().Be(NodeWatcherResult.NodeMayHaveDisconnected);
        }

        [Test]
        public async Task WhenWatchingTheNodeProcessingTheRequestIsStillAlive_AndTheWatchersConnectionToRedisGoesDown_ShouldReturnProcessingNodeIsLikelyDisconnected()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            await using var unstableRedisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder, guid);
            await using var stableRedisFacade = RedisFacadeBuilder.CreateRedisFacade(prefix: guid);
            
            var unstableRedisTransport = new HalibutRedisTransport(unstableRedisFacade);
            var stableRedisTransport = new HalibutRedisTransport(stableRedisFacade);
            
            var request = new RequestMessageBuilder(endpoint.ToString())
                .WithActivityId(requestActivityId)
                .Build();
            var pendingRequest = new RedisPendingRequest(request, log);
            
            // Start heartbeat sender
            await using var heartBeatSender = new NodeHeartBeatSender(
                endpoint,
                requestActivityId,
                stableRedisTransport,
                log,
                HalibutQueueNodeSendingPulses.RequestProcessorNode,
                defaultDelayBetweenPulses: TimeSpan.FromMilliseconds(200));
            
            // Mark request as collected so watcher proceeds to monitoring phase
            await pendingRequest.RequestHasBeenCollectedAndWillBeTransferred();
            
            // Start the watcher
            var watcherTask = NodeHeartBeatWatcher.WatchThatNodeProcessingTheRequestIsStillAlive(
                endpoint, 
                request, 
                pendingRequest, 
                unstableRedisTransport, 
                timeBetweenCheckingIfRequestWasCollected: TimeSpan.FromSeconds(1),
                log, 
                maxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline: TimeSpan.FromSeconds(5), // Short timeout for test
                CancellationToken);

            // Wait for initial heartbeats to establish baseline
            await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken);
            watcherTask.IsCompleted.Should().BeFalse();
            
            // Act - Kill the connection to stop heartbeats
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            
            // Assert
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(20)), watcherTask);
            watcherTask.IsCompleted.Should().BeTrue("Since it should have detected no heart beats have been sent for some time.");
            var result = await watcherTask;
            result.Should().Be(NodeWatcherResult.NodeMayHaveDisconnected);
        }
        
        [Test]
        public async Task WhenWatchingTheNodeProcessingTheRequestIsStillAlive_AndTheConnectionIsSuperStableAndWeStopWatching_WatcherShouldReturnNodeStayedConnected()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            
            var request = new RequestMessageBuilder(endpoint.ToString())
                .WithActivityId(requestActivityId)
                .Build();
            var pendingRequest = new RedisPendingRequest(request, log);
            await pendingRequest.RequestHasBeenCollectedAndWillBeTransferred();
            
            await using var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.RequestProcessorNode, TimeSpan.FromSeconds(1));
            
            using var watcherCts = new CancellationTokenSource();
            var watcherTask = NodeHeartBeatWatcher.WatchThatNodeProcessingTheRequestIsStillAlive(
                endpoint, 
                request, 
                pendingRequest, 
                redisTransport, 
                timeBetweenCheckingIfRequestWasCollected: TimeSpan.FromSeconds(1),
                log, 
                maxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline: TimeSpan.FromMinutes(1),
                watcherCts.Token);

            await Task.Delay(100);
            
            // Act
            await watcherCts.CancelAsync();
            
            // Assert
            var result = await watcherTask;
            result.Should().Be(NodeWatcherResult.NoDisconnectSeen);
        }

        [Test]
        public async Task SenderAndReceiverNodeTypes_ShouldUseDistinctChannels()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            
            var senderHeartbeatsReceived = new AsyncManualResetEvent(false);
            var receiverHeartbeatsReceived = new AsyncManualResetEvent(false);
            
            // Subscribe to sender heartbeats
            await using var senderSubscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.RequestSenderNode, async () =>
                {
                    await Task.CompletedTask;
                    senderHeartbeatsReceived.Set();
                }, CancellationToken);

            // Subscribe to receiver heartbeats
            await using var receiverSubscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.RequestProcessorNode, async () =>
                {
                    await Task.CompletedTask;
                    receiverHeartbeatsReceived.Set();
                }, CancellationToken);

            // Act - Create sender node heartbeat sender
            await using var senderHeartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.RequestSenderNode, defaultDelayBetweenPulses: TimeSpan.FromSeconds(1));
            
            // Wait for sender heartbeat
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5), CancellationToken), senderHeartbeatsReceived.WaitAsync());

            // Create receiver node heartbeat sender
            await using var receiverHeartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.RequestProcessorNode, defaultDelayBetweenPulses: TimeSpan.FromSeconds(1));
            
            // Wait for receiver heartbeat
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5), CancellationToken), receiverHeartbeatsReceived.WaitAsync());

            // Assert
            senderHeartbeatsReceived.IsSet.Should().BeTrue("Should have received sender heartbeat");
            receiverHeartbeatsReceived.IsSet.Should().BeTrue("Should have received receiver heartbeat");
        }

        [Test]
        public async Task SenderNodeHeartbeats_ShouldNotBeReceivedByReceiverSubscription()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            
            var receiverHeartbeatsReceived = new AsyncManualResetEvent(false);
            
            // Subscribe only to receiver heartbeats
            await using var receiverSubscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.RequestProcessorNode, async () =>
                {
                    await Task.CompletedTask;
                    receiverHeartbeatsReceived.Set();
                }, CancellationToken);

            // Act - Create sender node heartbeat sender (should not trigger receiver subscription)
            await using var senderHeartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.RequestSenderNode, defaultDelayBetweenPulses: TimeSpan.FromSeconds(1));
            
            // Wait to see if receiver subscription gets triggered (it shouldn't)
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(3), CancellationToken), receiverHeartbeatsReceived.WaitAsync());

            // Assert
            receiverHeartbeatsReceived.IsSet.Should().BeFalse("Should not have received sender heartbeat on receiver subscription");
        }
    }
} 
#endif
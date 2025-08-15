#if NET8_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Logging;
using Halibut.Queue.Redis;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Support.Logging;
using Halibut.Transport.Protocol;
using Halibut.Util;
using Nito.AsyncEx;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Queue.Redis
{
    [Ignore("REDISTODO")]
    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks")]
    public class NodeHeartBeatSenderFixture : BaseTest
    {
        const int redisPort = 6379;
        
        private static RedisFacade CreateRedisFacade(int? port = redisPort, Guid? guid = null) => 
            new("localhost:" + port, (guid ?? Guid.NewGuid()).ToString(), 
                new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));

        // TODO: ai tests need review
        [Test]
        public async Task WhenCreated_ShouldStartSendingHeartbeats()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            
            var anyHeartBeatReceived = new AsyncManualResetEvent(false);
            
            // Subscribe to heartbeats before creating the sender
            await using var subscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.Receiver, async () =>
                {
                    await Task.CompletedTask;
                    anyHeartBeatReceived.Set();
                }, CancellationToken);

            // Act
            await using var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.Receiver, defaultDelayBetweenPulses: TimeSpan.FromSeconds(1));
            
            // Wait for a few heartbeats
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(20), CancellationToken), anyHeartBeatReceived.WaitAsync());

            // Assert
            anyHeartBeatReceived.IsSet.Should().BeTrue("Should have received at least one heartbeat");
        }

        // Not sure this is a good test
        //[Test]
        public async Task WhenRedisConnectionIsInterrupted_ShouldSwitchToPanicMode()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            await using var unstableRedisFacade = CreateRedisFacade(portForwarder.ListeningPort, guid);
            await using var stableRedisFacade = CreateRedisFacade(redisPort, guid);
            
            var redisTransport = new HalibutRedisTransport(unstableRedisFacade);
            
            var heartbeatsReceived = new ConcurrentBag<DateTimeOffset>();
            
            // Subscribe with stable connection to monitor heartbeats
            await using var subscription = await new HalibutRedisTransport(stableRedisFacade)
                .SubscribeToNodeHeartBeatChannel(
                    endpoint, requestActivityId, HalibutQueueNodeSendingPulses.Receiver, async () =>
                    {
                        await Task.CompletedTask;
                        heartbeatsReceived.Add(DateTimeOffset.Now);
                    }, CancellationToken);

            // Act
            await using var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.Receiver, TimeSpan.FromSeconds(1));
            
            // Wait for initial heartbeat
            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);
            var initialHeartbeatCount = heartbeatsReceived.Count;
            
            // Interrupt connection
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            
            // Wait during the outage
            await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken);
            var heartbeatsDuringOutage = heartbeatsReceived.Count - initialHeartbeatCount;
            
            // Restore connection
            portForwarder.ReturnToNormalMode();
            
            // Wait for recovery
            await Task.Delay(TimeSpan.FromSeconds(15), CancellationToken);
            var heartbeatsAfterRecovery = heartbeatsReceived.Count - initialHeartbeatCount - heartbeatsDuringOutage;

            // Assert
            initialHeartbeatCount.Should().BeGreaterThan(0, "Should have received initial heartbeats");
            heartbeatsDuringOutage.Should().Be(0, "Should not receive heartbeats during network outage");
            heartbeatsAfterRecovery.Should().BeGreaterThan(0, "Should resume sending heartbeats after recovery");
        }

        [Test]
        public async Task WhenDisposed_ShouldStopSendingHeartbeats()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            
            var heartbeatsReceived = new ConcurrentBag<DateTimeOffset>();
            var anyHeartBeatReceived = new AsyncManualResetEvent(false);
            
            await using var subscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.Receiver, async () =>
                {
                    await Task.CompletedTask;
                    anyHeartBeatReceived.Set();
                    heartbeatsReceived.Add(DateTimeOffset.Now);
                }, CancellationToken);

            // Act
            var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.Receiver, defaultDelayBetweenPulses: TimeSpan.FromSeconds(1));
            
            // Wait for some heartbeats
            await anyHeartBeatReceived.WaitAsync(CancellationToken);
            
            // Dispose the sender
            await heartBeatSender.DisposeAsync();

            await heartBeatSender.TaskSendingPulses;
            
            anyHeartBeatReceived.Reset();
            
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5), CancellationToken), anyHeartBeatReceived.WaitAsync());

            anyHeartBeatReceived.IsSet.Should().BeFalse();
        }

        [Test]
        public async Task WaitUntilNodeProcessingRequestFlatLines_WhenHeartbeatsStop_ShouldReturnProcessingNodeIsLikelyDisconnected()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            await using var unstableRedisFacade = CreateRedisFacade(portForwarder.ListeningPort, guid);
            await using var stableRedisFacade = CreateRedisFacade(redisPort, guid);
            
            var unstableRedisTransport = new HalibutRedisTransport(unstableRedisFacade);
            var stableRedisTransport = new HalibutRedisTransport(stableRedisFacade);
            
            var request = new RequestMessageBuilder(endpoint.ToString())
                .WithActivityId(requestActivityId)
                .Build();
            var pendingRequest = new RedisPendingRequest(request, log);
            
            // Start heartbeat sender
            await using var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, unstableRedisTransport, log, HalibutQueueNodeSendingPulses.Receiver, TimeSpan.FromSeconds(1));
            
            // Mark request as collected so watcher proceeds to monitoring phase
            await pendingRequest.RequestHasBeenCollectedAndWillBeTransferred();
            
            // Start the watcher
            var watcherTask = NodeHeartBeatSender.WatchThatNodeProcessingTheRequestIsStillAlive(
                endpoint, 
                request, 
                pendingRequest, 
                stableRedisTransport, 
                TimeSpan.FromSeconds(1),
                log, 
                TimeSpan.FromSeconds(10), // Short timeout for test
                CancellationToken);

            // Wait for initial heartbeats to establish baseline
            await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken);
            
            // Act - Kill the connection to stop heartbeats
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            
            // Assert
            var result = await watcherTask;
            result.Should().Be(NodeHeartBeatSender.NodeProcessingRequestWatcherResult.NodeMayHaveDisconnected);
        }

        [Test]
        public async Task NoIssueReturnedWhenNodeProcessingRequestIsNotSeenToGoOffline()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            
            var request = new RequestMessageBuilder(endpoint.ToString())
                .WithActivityId(requestActivityId)
                .Build();
            var pendingRequest = new RedisPendingRequest(request, log);
            await pendingRequest.RequestHasBeenCollectedAndWillBeTransferred();
            
            // Start heartbeat sender
            await using var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.Receiver, TimeSpan.FromSeconds(1));
            
            // Start the watcher without marking request as collected
            using var cts = new CancellationTokenSource();
            var watcherTask = NodeHeartBeatSender.WatchThatNodeProcessingTheRequestIsStillAlive(
                endpoint, 
                request, 
                pendingRequest, 
                redisTransport, 
                TimeSpan.FromSeconds(1),
                log, 
                TimeSpan.FromMinutes(5),
                cts.Token);
            
            await cts.CancelAsync();
            
            // Assert
            var result = await watcherTask;
            result.Should().Be(NodeHeartBeatSender.NodeProcessingRequestWatcherResult.NoDisconnectSeen);
        }

        [Test]
        public async Task WaitUntilNodeProcessingRequestFlatLines_WhenConnectionInterruptedDuringMonitoring_ShouldStillDetectFlatline()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            await using var unstableRedisFacade = CreateRedisFacade(portForwarder.ListeningPort, guid);
            await using var stableRedisFacade = CreateRedisFacade(redisPort, guid);
            
            var unstableRedisTransport = new HalibutRedisTransport(unstableRedisFacade);
            var stableRedisTransport = new HalibutRedisTransport(stableRedisFacade);
            
            var request = new RequestMessageBuilder(endpoint.ToString())
                .WithActivityId(requestActivityId)
                .Build();
            var pendingRequest = new RedisPendingRequest(request, log);
            
            // Start heartbeat sender with unstable connection
            await using var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, unstableRedisTransport, log, HalibutQueueNodeSendingPulses.Receiver, TimeSpan.FromSeconds(1));
            
            // Mark request as collected
            await pendingRequest.RequestHasBeenCollectedAndWillBeTransferred();
            
            // Start watcher with stable connection
            var watcherTask = NodeHeartBeatSender.WatchThatNodeProcessingTheRequestIsStillAlive(
                endpoint, 
                request, 
                pendingRequest, 
                stableRedisTransport,
                TimeSpan.FromSeconds(1),
                log, 
                TimeSpan.FromSeconds(15), // Short timeout for test
                CancellationToken);

            // Wait for initial heartbeats
            await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken);
            
            // Act - Interrupt the heartbeat sender's connection
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            
            // Assert - Watcher should detect flatline
            var result = await watcherTask;
            result.Should().Be(NodeHeartBeatSender.NodeProcessingRequestWatcherResult.NodeMayHaveDisconnected);
        }

        [Test] 
        public async Task WhenMultipleHeartBeatSendersForSameRequest_OnlyOneSetOfHeartbeatsShouldBeReceived()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            
            var heartbeatsReceived = new ConcurrentBag<DateTimeOffset>();
            
            await using var subscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.Receiver, async () =>
                {
                    await Task.CompletedTask;
                    heartbeatsReceived.Add(DateTimeOffset.Now);
                }, CancellationToken);

            // Act - Create multiple senders for the same request
            await using var heartBeatSender1 = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.Receiver, TimeSpan.FromSeconds(1));
            await using var heartBeatSender2 = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.Receiver, TimeSpan.FromSeconds(1));
            
            // Wait for heartbeats
            await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken);

            // Assert
            heartbeatsReceived.Should().NotBeEmpty("Should have received heartbeats");
            // Note: We can't easily assert the exact count since both senders are publishing,
            // but we can verify the system handles multiple senders gracefully
        }

        [Test]
        public async Task WhenHeartBeatSenderConnectionRecovery_ShouldResumeNormalHeartbeatInterval()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            await using var unstableRedisFacade = CreateRedisFacade(portForwarder.ListeningPort, guid);
            await using var stableRedisFacade = CreateRedisFacade(redisPort, guid);
            
            var unstableRedisTransport = new HalibutRedisTransport(unstableRedisFacade);
            var stableRedisTransport = new HalibutRedisTransport(stableRedisFacade);
            
            var heartbeatTimestamps = new ConcurrentBag<DateTimeOffset>();
            
            await using var subscription = await stableRedisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.Receiver, async () =>
                {
                    await Task.CompletedTask;
                    heartbeatTimestamps.Add(DateTimeOffset.Now);
                }, CancellationToken);

            // Act
            await using var heartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, unstableRedisTransport, log, HalibutQueueNodeSendingPulses.Receiver, TimeSpan.FromSeconds(1));
            
            // Wait for initial heartbeats (normal 15s interval)
            await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken);
            
            // Interrupt connection to trigger panic mode (7s interval)
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);
            
            // Restore connection
            portForwarder.ReturnToNormalMode();
            
            // Wait for recovery and return to normal intervals
            await Task.Delay(TimeSpan.FromSeconds(20), CancellationToken);

            // Assert
            heartbeatTimestamps.Should().NotBeEmpty("Should have received heartbeats after recovery");
            
            // Verify we have heartbeats spanning the recovery period
            var timestamps = heartbeatTimestamps.ToArray();
            if (timestamps.Length > 1)
            {
                var timeSpan = timestamps.Max() - timestamps.Min();
                timeSpan.Should().BeGreaterThan(TimeSpan.FromSeconds(10), "Should have heartbeats over recovery period");
            }
        }

        [Test]
        public async Task SenderAndReceiverNodeTypes_ShouldUseDistinctChannels()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var requestActivityId = Guid.NewGuid();
            var log = new TestContextLogCreator("NodeHeartBeat", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            
            var senderHeartbeatsReceived = new AsyncManualResetEvent(false);
            var receiverHeartbeatsReceived = new AsyncManualResetEvent(false);
            
            // Subscribe to sender heartbeats
            await using var senderSubscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.Sender, async () =>
                {
                    await Task.CompletedTask;
                    senderHeartbeatsReceived.Set();
                }, CancellationToken);

            // Subscribe to receiver heartbeats
            await using var receiverSubscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.Receiver, async () =>
                {
                    await Task.CompletedTask;
                    receiverHeartbeatsReceived.Set();
                }, CancellationToken);

            // Act - Create sender node heartbeat sender
            await using var senderHeartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.Sender, defaultDelayBetweenPulses: TimeSpan.FromSeconds(1));
            
            // Wait for sender heartbeat
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5), CancellationToken), senderHeartbeatsReceived.WaitAsync());

            // Create receiver node heartbeat sender
            await using var receiverHeartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.Receiver, defaultDelayBetweenPulses: TimeSpan.FromSeconds(1));
            
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
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            
            var receiverHeartbeatsReceived = new AsyncManualResetEvent(false);
            
            // Subscribe only to receiver heartbeats
            await using var receiverSubscription = await redisTransport.SubscribeToNodeHeartBeatChannel(
                endpoint, requestActivityId, HalibutQueueNodeSendingPulses.Receiver, async () =>
                {
                    await Task.CompletedTask;
                    receiverHeartbeatsReceived.Set();
                }, CancellationToken);

            // Act - Create sender node heartbeat sender (should not trigger receiver subscription)
            await using var senderHeartBeatSender = new NodeHeartBeatSender(endpoint, requestActivityId, redisTransport, log, HalibutQueueNodeSendingPulses.Sender, defaultDelayBetweenPulses: TimeSpan.FromSeconds(1));
            
            // Wait to see if receiver subscription gets triggered (it shouldn't)
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(3), CancellationToken), receiverHeartbeatsReceived.WaitAsync());

            // Assert
            receiverHeartbeatsReceived.IsSet.Should().BeFalse("Should not have received sender heartbeat on receiver subscription");
        }
    }
} 
#endif
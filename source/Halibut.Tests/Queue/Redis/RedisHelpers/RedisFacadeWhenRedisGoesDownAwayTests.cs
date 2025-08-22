#if NET8_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support;
using NUnit.Framework;

namespace Halibut.Tests.Queue.Redis.RedisHelpers
{
    [RedisTest]
    public class RedisFacadeWhenRedisGoesDownAwayTests : BaseTest
    {
        [Test]
        public async Task WhenTheEstablishedConnectionToRedisBrieflyGoesDown_WeCanEventuallyInteractWithRedisAgain()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder);

            await redisFacade.SetString("foo", "bar", TimeSpan.FromMinutes(1), CancellationToken);

            (await redisFacade.GetString("foo", CancellationToken)).Should().Be("bar");

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            await redisFacade.GetString("foo", CancellationToken);
        }

        [Test]
        public async Task WhenTheEstablishedConnectionToRedisBrieflyGoesDown_WeCanPublishToAChannel()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);

            var guid = Guid.NewGuid();
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder, guid);
            
            await using var redisFacadeReliable = RedisFacadeBuilder.CreateRedisFacade(prefix: guid);

            var receivedMessages = new ConcurrentBag<string>();
            await using var subscription = await redisFacadeReliable.SubscribeToChannel("test-channel", async message =>
            {
                await Task.CompletedTask;
                receivedMessages.Add(message.Message!);
            }, CancellationToken);
            
            // Establish connection first
            await redisFacade.SetString("connection", "established", TimeSpan.FromMinutes(1), CancellationToken);
            

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // Assert
            await redisFacade.PublishToChannel("test-channel", "test-message", CancellationToken);
            
            // Check that publish actually happened.
            await ShouldEventually.Eventually(() => receivedMessages.Should().Contain("test-message"), TimeSpan.FromSeconds(10), CancellationToken);
        }

        [Test]
        public async Task WhenTheEstablishedConnectionToRedisBrieflyGoesDown_WeCanImmediatelySetInHash()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder);

            // Establish connection first
            await redisFacade.SetString("connection", "established", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            // Assert
            await redisFacade.SetInHash("test-hash", "test-field", "test-value", TimeSpan.FromMinutes(1), CancellationToken);
            
            // Check that the value was set.
            var retrievedValue = await redisFacade.TryGetAndDeleteFromHash("test-hash", "test-field", CancellationToken);
            retrievedValue.Should().Be("test-value");
        }

        [Test]
        public async Task WhenTheEstablishedConnectionToRedisBrieflyGoesDown_WeCanImmediatelyTryGetAndDeleteFromHash()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder);

            // Establish connection and set up test data
            await redisFacade.SetInHash("test-hash", "test-field", "test-value", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            var result = await redisFacade.TryGetAndDeleteFromHash("test-hash", "test-field", CancellationToken);
            result.Should().Be("test-value");
        }

        [Test]
        public async Task WhenTheEstablishedConnectionToRedisBrieflyGoesDown_WeCanImmediatelyListRightPush()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder);

            // Establish connection first
            await redisFacade.SetString("connection", "established", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            await redisFacade.ListRightPushAsync("test-list", "test-item", TimeSpan.FromMinutes(1), CancellationToken);
            
            // Check we actually added something to the queue.
            var poppedValue = await redisFacade.ListLeftPopAsync("test-list", CancellationToken);
            poppedValue.Should().Be("test-item");
        }

        [Test]
        public async Task WhenTheEstablishedConnectionToRedisBrieflyGoesDown_WeCanImmediatelyListLeftPop()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder);

            // Establish connection and set up test data
            await redisFacade.ListRightPushAsync("test-list", "test-item", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            var result = await redisFacade.ListLeftPopAsync("test-list", CancellationToken);
            result.Should().Be("test-item");
        }

        [Test]
        public async Task WhenTheEstablishedConnectionToRedisBrieflyGoesDown_WeCanImmediatelySetString()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder);

            // Establish connection first
            await redisFacade.SetString("connection", "established", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            await redisFacade.SetString("test-key", "test-value", TimeSpan.FromMinutes(1), CancellationToken);
            
            // Verify we can read back the string
            var retrievedValue = await redisFacade.GetString("test-key", CancellationToken);
            retrievedValue.Should().Be("test-value");
        }

        [Test]
        public async Task WhenTheEstablishedConnectionToRedisBrieflyGoesDown_WeCanImmediatelyGetString()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder);

            // Establish connection and set up test data
            await redisFacade.SetString("test-key", "test-value", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            var result = await redisFacade.GetString("test-key", CancellationToken);
            result.Should().Be("test-value");
        }

        [Test]
        public async Task WhenTheEstablishedConnectionToRedisBrieflyGoesDown_WeCanImmediatelyHashContainsKey()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder);

            // Establish connection and set up test data
            await redisFacade.SetInHash("test-hash", "test-field", "test-value", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            var exists = await redisFacade.HashContainsKey("test-hash", "test-field", CancellationToken);
            exists.Should().BeTrue();
        }

        [Test]
        public async Task WhenTheConnectionToRedisHasBeenEstablished_AndIsLaterTerminated_AndThenWeTryToSubscribe_WhenTheConnectionIsRestored_WeCanReceiveMessages()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            var guid = Guid.NewGuid();
            await using var redisViaPortForwarder = RedisFacadeBuilder.CreateRedisFacade(portForwarder: portForwarder, prefix: guid);
            await using var redisStableConnection = RedisFacadeBuilder.CreateRedisFacade(prefix: guid);
            await redisStableConnection.PublishToChannel("bob", "establishing connection to redis", CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();

            var msgs = new ConcurrentBag<string>();
            using var subscribeToChannelTask = redisViaPortForwarder.SubscribeToChannel("bob", async message =>
            {
                await Task.CompletedTask;
                msgs.Add(message.Message!);
            }, CancellationToken);

            // Give everything enough time to have a crack at trying to subscribe to messages.
            await Task.Delay(2000);
            await redisStableConnection.PublishToChannel("bob", "MISSED", CancellationToken);

            // Just in case the subscriber reconnects faster than redis publishes the MISSED message. 
            await Task.Delay(2000);

            portForwarder.ReturnToNormalMode();

            // Keep going around the loop until we recieve something
            while (msgs.Count == 0)
            {
                Logger.Information("Trying again");
                await redisStableConnection.PublishToChannel("bob", "RECONNECT", CancellationToken);
                await Task.Delay(1000);
            }

            msgs.Should().Contain("RECONNECT", "Since the subscriber should eventually connect back up");
            msgs.Should().NotContain("MISSED", "Since this was sent when the subscriber could not have been connected. " +
                                               "If this is seen maybe the test itself has a bug.");
        }

        [Test]
        public async Task WhenTheConnectionIsNeverEstablished_AndWeTryToSubscribe_WhenTheConnectionIsRestored_WeCanReceiveMessages()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            var guid = Guid.NewGuid();
            await using var redisViaPortForwarder = RedisFacadeBuilder.CreateRedisFacade(portForwarder: portForwarder, prefix: guid);
            await using var redisStableConnection = RedisFacadeBuilder.CreateRedisFacade(prefix: guid);

            portForwarder.EnterKillNewAndExistingConnectionsMode();

            var msgs = new ConcurrentBag<string>();
            using var subscribeToChannelTask = redisViaPortForwarder.SubscribeToChannel("bob", async message =>
            {
                await Task.CompletedTask;
                msgs.Add(message.Message!);
            }, CancellationToken);

            // Give everything enough time to have a crack at trying to subscribe to messages.
            await Task.Delay(2000);
            await redisStableConnection.PublishToChannel("bob", "MISSED", CancellationToken);

            // Just in case the subscriber reconnects faster than the publish call. 
            await Task.Delay(2000);

            portForwarder.ReturnToNormalMode();

            // Keep going around the loop until we recieve something
            while (msgs.Count == 0)
            {
                Logger.Information("Trying again");
                await redisStableConnection.PublishToChannel("bob", "RECONNECT", CancellationToken);
                await Task.Delay(1000);
            }

            msgs.Should().Contain("RECONNECT", "Since the subscriber should eventually connect up");
            msgs.Should().NotContain("MISSED", "Since this was sent when the subscriber could not have been connected. " +
                                               "If this is seen maybe the test itself has a bug.");
        }

        [Test]
        public async Task WhenSubscribedAndTheConnectionGoesDown_WhenTheConnectionIsRestored_MessagesCanEventuallyBeSentToTheSubscriberAgain()
        {
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);

            var guid = Guid.NewGuid();
            await using var redisViaPortForwarder = RedisFacadeBuilder.CreateRedisFacade(portForwarder: portForwarder, prefix: guid);
            await using var redisStableConnection = RedisFacadeBuilder.CreateRedisFacade(prefix: guid);

            var msgs = new ConcurrentBag<string>();
            await using var channel = await redisViaPortForwarder.SubscribeToChannel("bob", async message =>
            {
                await Task.CompletedTask;
                msgs.Add(message.Message!);
            }, CancellationToken);

            // Check both sides can publish.
            await redisViaPortForwarder.PublishToChannel("bob", "hello unstable", CancellationToken);
            await redisStableConnection.PublishToChannel("bob", "hello stable", CancellationToken);

            await ShouldEventually.Eventually(() => msgs.Should().BeEquivalentTo("hello unstable", "hello stable"), TimeSpan.FromSeconds(10), CancellationToken);
            

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            // The stable connection should still be able to publish to redis.
            // But the subscriber on the unstable connection will not got the message.
            await redisStableConnection.PublishToChannel("bob", "MISSED", CancellationToken);
            await Task.Delay(1111); // Delay for some amount of time for redis to publish MISSED this won't be received since the connection is down.
            portForwarder.ReturnToNormalMode();

            while (msgs.Count <= 2)
            {
                CancellationToken.ThrowIfCancellationRequested();
                Logger.Information("Trying again");
                await redisStableConnection.PublishToChannel("bob", "RECONNECT", CancellationToken);
                await Task.Delay(1000);
            }

            msgs.Should().Contain("RECONNECT", "Since the subscriber should eventually be re-connected");
            msgs.Should().NotContain("MISSED", "Since this was sent when the subscriber could not have been connected. " +
                                               "If this is seen maybe the test itself has a bug.");
        }
    }
}
#endif
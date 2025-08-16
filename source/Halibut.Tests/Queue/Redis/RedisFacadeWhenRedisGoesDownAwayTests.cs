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
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Queue.Redis;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestSetup.Redis;
using Halibut.Util;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Queue.Redis
{
    [RedisTest]
    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks")]
    public class RedisFacadeWhenRedisGoesDownAwayTests : BaseTest
    {
        static RedisFacade CreateRedisFacade(int? port = 0, Guid? guid = null)
        {
            port = port == 0 ? RedisPort.Port() : port; 
            return new RedisFacade("localhost:" + port, (guid ?? Guid.NewGuid()).ToString(), new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_SometimeLaterOnWeCanDoBasicCalls()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            await redisFacade.SetString("foo", "bar", TimeSpan.FromMinutes(1), CancellationToken);

            (await redisFacade.GetString("foo", CancellationToken)).Should().Be("bar");

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // After a short delay it does seem to work again.
            await Task.Delay(1000);

            await redisFacade.GetString("foo", CancellationToken);
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyDoBasicCalls()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            await redisFacade.SetString("foo", "bar", TimeSpan.FromMinutes(1), CancellationToken);

            (await redisFacade.GetString("foo", CancellationToken)).Should().Be("bar");

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // No delay here

            await redisFacade.GetString("foo", CancellationToken);
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyPublishToChannel()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection first
            await redisFacade.SetString("connection", "established", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // No delay here - should retry and succeed
            await redisFacade.PublishToChannel("test-channel", "test-message", CancellationToken);
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelySetInHash()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection first
            await redisFacade.SetString("connection", "established", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // No delay here - should retry and succeed
            await redisFacade.SetInHash("test-hash", "test-field", "test-value", TimeSpan.FromMinutes(1), CancellationToken);
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyTryGetAndDeleteFromHash()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection and set up test data
            await redisFacade.SetInHash("test-hash", "test-field", "test-value", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // No delay here - should retry and succeed
            var result = await redisFacade.TryGetAndDeleteFromHash("test-hash", "test-field", CancellationToken);
            result.Should().Be("test-value");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyListRightPush()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection first
            await redisFacade.SetString("connection", "established", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // No delay here - should retry and succeed
            await redisFacade.ListRightPushAsync("test-list", "test-item", TimeSpan.FromMinutes(1), CancellationToken);
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyListLeftPop()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection and set up test data
            await redisFacade.ListRightPushAsync("test-list", "test-item", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // No delay here - should retry and succeed
            var result = await redisFacade.ListLeftPopAsync("test-list", CancellationToken);
            result.Should().Be("test-item");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelySetString()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection first
            await redisFacade.SetString("connection", "established", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // No delay here - should retry and succeed
            await redisFacade.SetString("test-key", "test-value", TimeSpan.FromMinutes(1), CancellationToken);
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyGetString()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection and set up test data
            await redisFacade.SetString("test-key", "test-value", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // No delay here - should retry and succeed
            var result = await redisFacade.GetString("test-key", CancellationToken);
            result.Should().Be("test-value");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyHashContainsKey()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection and set up test data
            await redisFacade.SetInHash("test-hash", "test-field", "test-value", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // No delay here - should retry and succeed
            var exists = await redisFacade.HashContainsKey("test-hash", "test-field", CancellationToken);
            exists.Should().BeTrue();
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndWeTryToSubscribe_WhenTheConnectionIsRestored_WeCanReceiveMessages()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();
            var redisLogCreator = new TestContextLogCreator("Redis", LogLevel.Trace);
            var guid = Guid.NewGuid().ToString();
            await using var redisViaPortForwarder = new RedisFacade("localhost:" + portForwarder.ListeningPort, guid, redisLogCreator.CreateNewForPrefix("Unstable"));
            await using var redisStableConnection = new RedisFacade("localhost:" + RedisPort.Port(), guid, redisLogCreator.CreateNewForPrefix("Stable"));

            await redisViaPortForwarder.SetString("Establish connection", "before we subscribe", TimeSpan.FromMinutes(1), CancellationToken);

            portForwarder.EnterKillNewAndExistingConnectionsMode();

            var msgs = new ConcurrentBag<string>();
            var subscribeToChannelTask = redisViaPortForwarder.SubscribeToChannel("bob", async message =>
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
        public async Task WhenTheConnectionIsNeverEstablished_AndWeTryToSubscribe_WhenTheConnectionIsRestored_WeCanReceiveMessages()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();
            var redisLogCreator = new TestContextLogCreator("Redis", LogLevel.Trace);
            var guid = Guid.NewGuid().ToString();
            await using var redisViaPortForwarder = new RedisFacade("localhost:" + portForwarder.ListeningPort, guid, redisLogCreator.CreateNewForPrefix("Unstable"));
            await using var redisStableConnection = new RedisFacade("localhost:" + RedisPort.Port(), guid, redisLogCreator.CreateNewForPrefix("Stable"));

            portForwarder.EnterKillNewAndExistingConnectionsMode();

            var msgs = new ConcurrentBag<string>();
            var subscribeToChannelTask = redisViaPortForwarder.SubscribeToChannel("bob", async message =>
            {
                await Task.CompletedTask;
                msgs.Add(message.Message!);
            }, CancellationToken);

            await using var _ = new FuncAsyncDisposable(() => Try.IgnoringError(async () => await (await subscribeToChannelTask).DisposeAsync()));

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

        [WindowsTest]
        public async Task WhenSubscribedAndTheConnectionGoesDown_WhenTheConnectionIsRestored_MessagesCanEventuallyBeSentToTheSubscriberAgain()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(RedisPort.Port(), Logger).Build();

            var redisLogCreator = new TestContextLogCreator("Redis", LogLevel.Trace);

            var guid = Guid.NewGuid().ToString();
            await using var redisViaPortForwarder = new RedisFacade("localhost:" + portForwarder.ListeningPort, guid, redisLogCreator.CreateNewForPrefix("Unstable"));

            await using var redisStableConnection = new RedisFacade("localhost:" + RedisPort.Port(), guid, redisLogCreator.CreateNewForPrefix("Stable"));

            var msgs = new ConcurrentBag<string>();
            await using var channel = await redisViaPortForwarder.SubscribeToChannel("bob", async message =>
            {
                await Task.CompletedTask;
                msgs.Add(message.Message!);
            }, CancellationToken);

            // Check both sides can publish.
            await redisViaPortForwarder.PublishToChannel("bob", "hello unstable", CancellationToken);
            await redisStableConnection.PublishToChannel("bob", "hello stable", CancellationToken);
            await Task.Delay(1000); // TODO better
            msgs.Should().BeEquivalentTo("hello unstable", "hello stable");

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            // The stable connection should still be able to publish to redis.
            // But the subscriber on the unstable connection will not got the message.
            await redisStableConnection.PublishToChannel("bob", "MISSED", CancellationToken);
            await Task.Delay(1111);
            portForwarder.ReturnToNormalMode();

            while (msgs.Count <= 2)
            {
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
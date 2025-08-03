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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Logging;
using Halibut.Queue.Redis;
using Halibut.Tests.Support.Logging;
using Halibut.Util;
using NUnit.Framework;
using Octopus.TestPortForwarder;
using StackExchange.Redis;

namespace Halibut.Tests.Queue.Redis
{
    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks")]
    public class RedisFacadeWhenRedisGoesDownAwayTests : BaseTest
    {
        private static RedisFacade CreateRedisFacade(int port) => new("localhost:" + port, Guid.NewGuid().ToString(), new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));

        
        int redisPort = 6379;
        
        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_SometimeLaterOnWeCanDoBasicCalls()
        {
            
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            
            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            await redisFacade.SetString("foo", "bar");

            (await redisFacade.GetString("foo")).Should().Be("bar");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // After a short delay it does seem to work again.
            await Task.Delay(1000);
            
            await redisFacade.GetString("foo");
        }
        
        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyDoBasicCalls()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            
            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            await redisFacade.SetString("foo", "bar");

            (await redisFacade.GetString("foo")).Should().Be("bar");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            // No delay here
            
            await redisFacade.GetString("foo");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyPublishToChannel()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            
            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection first
            await redisFacade.SetString("connection", "established");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            // No delay here - should retry and succeed
            await redisFacade.PublishToChannel("test-channel", "test-message");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelySetInHash()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            
            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection first
            await redisFacade.SetString("connection", "established");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            // No delay here - should retry and succeed
            await redisFacade.SetInHash("test-hash", "test-field", "test-value");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyTryGetAndDeleteFromHash()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            
            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection and set up test data
            await redisFacade.SetInHash("test-hash", "test-field", "test-value");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            // No delay here - should retry and succeed
            var result = await redisFacade.TryGetAndDeleteFromHash("test-hash", "test-field");
            result.Should().Be("test-value");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyListRightPush()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            
            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection first
            await redisFacade.SetString("connection", "established");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            // No delay here - should retry and succeed
            await redisFacade.ListRightPushAsync("test-list", "test-item");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyListLeftPop()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            
            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection and set up test data
            await redisFacade.ListRightPushAsync("test-list", "test-item");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            // No delay here - should retry and succeed
            var result = await redisFacade.ListLeftPopAsync("test-list");
            result.Should().Be("test-item");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelySetString()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            
            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection first
            await redisFacade.SetString("connection", "established");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            // No delay here - should retry and succeed
            await redisFacade.SetString("test-key", "test-value");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndThenReConnected_WeCanImmediatelyGetString()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            
            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            // Establish connection and set up test data
            await redisFacade.SetString("test-key", "test-value");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();
            
            // No delay here - should retry and succeed
            var result = await redisFacade.GetString("test-key");
            result.Should().Be("test-value");
        }

        [Test]
        public async Task WhenTheConnectionHasBeenEstablishedAndThenTerminated_AndWeTryToSubscribe_WhenTheConnectionIsRestored_WeCanReceiveMessages()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            var redisLogCreator = new TestContextLogCreator("Redis", LogLevel.Trace);
            var guid = Guid.NewGuid().ToString();
            await using var redisViaPortForwarder = new RedisFacade("localhost:" + portForwarder.ListeningPort, guid, redisLogCreator.CreateNewForPrefix("Unstable"));
            await using var redisStableConnection = new RedisFacade("localhost:" + redisPort, guid, redisLogCreator.CreateNewForPrefix("Stable"));

            await redisViaPortForwarder.SetString("Establish connection", "before we subscribe");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            
            var msgs = new ConcurrentBag<string>();
            var subscribeToChannelTask = redisViaPortForwarder.SubscribeToChannel("bob", async message =>
            {
                await Task.CompletedTask;
                msgs.Add(message.Message!);
            }, CancellationToken);
            

            // Give everything enough time to have a crack at trying to subscribe to messages.
            await Task.Delay(2000);
            await redisStableConnection.PublishToChannel("bob", "MISSED");
            
            // Just in case the subscriber reconnects faster than the publish call. 
            await Task.Delay(2000);
            
            portForwarder.ReturnToNormalMode();

            // Keep going around the loop until we recieve something
            while (msgs.Count == 0)
            {
                Logger.Information("Trying again");
                await redisStableConnection.PublishToChannel("bob", "RECONNECT");
                await Task.Delay(1000);
            }

            msgs.Should().Contain("RECONNECT", "Since the subscriber should eventually connect up");
            msgs.Should().NotContain("MISSED", "Since this was sent when the subscriber could not have been connected. " +
                                               "If this is seen maybe the test itself has a bug.");
        }
        
        [Test]
        public async Task WhenTheConnectionIsNeverEstablished_AndWeTryToSubscribe_WhenTheConnectionIsRestored_WeCanReceiveMessages()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            var redisLogCreator = new TestContextLogCreator("Redis", LogLevel.Trace);
            var guid = Guid.NewGuid().ToString();
            await using var redisViaPortForwarder = new RedisFacade("localhost:" + portForwarder.ListeningPort, guid, redisLogCreator.CreateNewForPrefix("Unstable"));
            await using var redisStableConnection = new RedisFacade("localhost:" + redisPort, guid, redisLogCreator.CreateNewForPrefix("Stable"));

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
            await redisStableConnection.PublishToChannel("bob", "MISSED");
            
            // Just in case the subscriber reconnects faster than the publish call. 
            await Task.Delay(2000);
            
            portForwarder.ReturnToNormalMode();

            // Keep going around the loop until we recieve something
            while (msgs.Count == 0)
            {
                Logger.Information("Trying again");
                await redisStableConnection.PublishToChannel("bob", "RECONNECT");
                await Task.Delay(1000);
            }

            msgs.Should().Contain("RECONNECT", "Since the subscriber should eventually connect up");
            msgs.Should().NotContain("MISSED", "Since this was sent when the subscriber could not have been connected. " +
                                               "If this is seen maybe the test itself has a bug.");
        }
        
        [Test]
        public async Task WhenSubscribedAndTheConnectionGoesDown_WhenTheConnectionIsRestored_MessagesCanEventuallyBeSentToTheSubscriberAgain()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();

            var redisLogCreator = new TestContextLogCreator("Redis", LogLevel.Trace);
            
            var guid = Guid.NewGuid().ToString();
            await using var redisViaPortForwarder = new RedisFacade("localhost:" + portForwarder.ListeningPort, guid, redisLogCreator.CreateNewForPrefix("Unstable"));
            
            await using var redisStableConnection = new RedisFacade("localhost:" + redisPort, guid, redisLogCreator.CreateNewForPrefix("Stable"));

            var msgs = new ConcurrentBag<string>();
            await using var channel = await redisViaPortForwarder.SubscribeToChannel("bob", async message =>
            {
                await Task.CompletedTask;
                msgs.Add(message.Message!);
            }, CancellationToken); 
            
            // Check both sides can publish.
            await redisViaPortForwarder.PublishToChannel("bob", "hello unstable");
            await redisStableConnection.PublishToChannel("bob", "hello stable");
            await Task.Delay(1000); // TODO better
            msgs.Should().BeEquivalentTo("hello unstable", "hello stable");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            // The stable connection should still be able to publish to redis.
            // But the subscriber on the unstable connection will not got the message.
            await redisStableConnection.PublishToChannel("bob", "MISSED");
            await Task.Delay(1111);
            portForwarder.ReturnToNormalMode();

            while (msgs.Count <= 2)
            {
                Logger.Information("Trying again");
                await redisStableConnection.PublishToChannel("bob", "RECONNECT");
                await Task.Delay(1000);
            }

            msgs.Should().Contain("RECONNECT", "Since the subscriber should eventually be re-connected");
            msgs.Should().NotContain("MISSED", "Since this was sent when the subscriber could not have been connected. " +
                                               "If this is seen maybe the test itself has a bug.");
        }


    }
}
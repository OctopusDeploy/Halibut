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
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Logging;
using Halibut.Queue.Redis;
using Halibut.Tests.Support.Logging;
using NUnit.Framework;
using Octopus.TestPortForwarder;
using StackExchange.Redis;

namespace Halibut.Tests.Queue.Redis
{
    public class RedisFacadeWhenRedisGoesDownAwayTests : BaseTest
    {
        private static RedisFacade CreateRedisFacade(int port) => new("localhost:" + port, Guid.NewGuid().ToString(), new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));

        
        int redisPort = 6379;
        
        [Test]
        public async Task WhenTheConnectionToRedisBrieflyGoesDown_FutureRequestsAShortTimeLaterCanBeExecuted()
        {
            
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            
            var configurationOptions = new ConfigurationOptions();
            configurationOptions.EndPoints.Add("localhost:" + redisPort);
            
            await using var redisFacade = CreateRedisFacade(portForwarder.ListeningPort);

            await redisFacade.SetString("foo", "bar");

            (await redisFacade.GetString("foo")).Should().Be("bar");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            portForwarder.ReturnToNormalMode();

            // After a short delay it does seem to work again.
            await Task.Delay(100);
            
            await redisFacade.GetString("foo");
            

            await Task.CompletedTask;
        }

        [Test]
        public async Task WhenSubWhenRedisCanNotBeReached()
        {

            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();

            var configurationOptions = new ConfigurationOptions();
            configurationOptions.EndPoints.Add("localhost:" + portForwarder.ListeningPort);
            configurationOptions.AbortOnConnectFail = false;

            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("Unstable");
            log.Write(EventType.Diagnostic, "Hello from log");
            
            await using var redisViaPortForwarder = new RedisFacade(configurationOptions, Guid.NewGuid().ToString(), log);
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();


            await using var channel = await redisViaPortForwarder.SubscribeToChannel("bob", async message =>
            {
                await Task.CompletedTask;
            });
        }
        
        [Test]
        public async Task WhenSubScribedToAChannelAndRedisConnectGoesAwayWhatHappens()
        {
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();

            var redisLogCreator = new TestContextLogCreator("Redis", LogLevel.Trace);
            
            var guid = Guid.NewGuid().ToString();
            await using var redisViaPortForwarder = new RedisFacade("localhost:" + portForwarder.ListeningPort, guid, redisLogCreator.CreateNewForPrefix("Unstable"));
            
            await using var redisStableConnection = new RedisFacade("localhost:" + redisPort, guid, redisLogCreator.CreateNewForPrefix("Stable"));

            var msgs = new List<string>();

            await using var channel = await redisViaPortForwarder.SubscribeToChannel("bob", async message =>
            {
                await Task.CompletedTask;
                msgs.Add(message.Message!);
            });

            await Task.Delay(1000);
            await redisViaPortForwarder.PublishToChannel("bob", "hello");
            await redisStableConnection.PublishToChannel("bob", "hello stable");
            msgs.Should().BeEquivalentTo("hello", "hello stable");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            await Task.Delay(5000);
            portForwarder.ReturnToNormalMode();

            
            while (msgs.Count <= 2)
            {
                Logger.Information("Trying again");
                await redisStableConnection.PublishToChannel("bob", "hello");
                await Task.Delay(5000);
            }
            
            

            
            
            await Task.CompletedTask;
        }


    }
}
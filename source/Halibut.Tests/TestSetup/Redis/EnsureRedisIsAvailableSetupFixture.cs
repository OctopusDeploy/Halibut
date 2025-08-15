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
using System.Runtime.InteropServices;
using Halibut.Tests.Support;
using Serilog;
using StackExchange.Redis;

namespace Halibut.Tests.TestSetup.Redis
{
    public class EnsureRedisIsAvailableSetupFixture : ISetupFixture
    {
        CreateRedisDockerContainerForTests? redisContainer = null;
        public void OneTimeSetUp(ILogger logger)
        {
#if NETFRAMEWORK
            return;
#else
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool shouldCreateRedis = !isWindows && TeamCityDetection.IsRunningInTeamCity();

            if (!TeamCityDetection.IsRunningInTeamCity())
            {
                if (!isWindows)
                {
                    // Does the user already have redis running on the normal port?
                    try
                    {
                        using var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
                        var ts = multiplexer.GetDatabase().Ping();
                        RedisPort.SetPort(6379);
                    }
                    catch
                    {
                        shouldCreateRedis = true;
                    }
                }
            }
            
            if (shouldCreateRedis)
            {
                redisContainer = new CreateRedisDockerContainerForTests(logger);
                redisContainer.InitializeAsync().GetAwaiter().GetResult();
                RedisPort.SetPort(redisContainer.container!.RedisPort);
                logger.Information("RedisPort is: {RedisPort}", RedisPort.Port());
            }
#endif
        }

        public void OneTimeTearDown(ILogger logger)
        {

            if(redisContainer != null) redisContainer.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
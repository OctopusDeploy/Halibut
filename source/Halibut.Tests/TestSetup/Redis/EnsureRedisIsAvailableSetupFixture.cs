using System;
using System.Runtime.InteropServices;
using Halibut.Tests.Support;
using Serilog;
using StackExchange.Redis;

namespace Halibut.Tests.TestSetup.Redis
{
    public class EnsureRedisIsAvailableSetupFixture : ISetupFixture
    {
        public static bool WillRunRedisTests =>
#if NETFRAMEWORK
            false;
#else
            !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                || !TeamCityDetection.IsRunningInTeamCity();
#endif
        
        static readonly int RedisPortToTry = EnvironmentVariableReaderHelper.TryReadIntFromEnvironmentVariable("HALIBUT_REDIS_PORT") ?? 6379;
        static readonly string RedisHost = Environment.GetEnvironmentVariable("HALIBUT_REDIS_HOST") ?? "localhost";
        CreateRedisDockerContainerForTests? redisContainer = null;
        public void OneTimeSetUp(ILogger logger)
        {
            if (!WillRunRedisTests) return;
            
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool shouldCreateRedis = WillRunRedisTests;

            if (!TeamCityDetection.IsRunningInTeamCity())
            {
                // Does the user already have redis running on the normal port?
                try
                {
                    using var multiplexer = ConnectionMultiplexer.Connect(RedisHost + ":" + RedisPortToTry);
                    var ts = multiplexer.GetDatabase().Ping();
                    RedisTestHost.SetPort(RedisPortToTry);
                    RedisTestHost.RedisHost = RedisHost;
                    logger.Information("Able to connect to redis using {Host}:{Port}", RedisHost, RedisPortToTry);
                    return;
                }
                catch
                {
                    shouldCreateRedis = true;
                }
            }
            
            if (shouldCreateRedis)
            {
                redisContainer = new CreateRedisDockerContainerForTests(logger);
                redisContainer.InitializeAsync().GetAwaiter().GetResult();
                RedisTestHost.SetPort(redisContainer.container!.RedisPort);
                logger.Information("RedisPort is: {RedisPort}", RedisTestHost.Port());
            }
        }

        public void OneTimeTearDown(ILogger logger)
        {

            if(redisContainer != null) redisContainer.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
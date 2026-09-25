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
        // Matches the password in the docker run command in docs/RunningRedisLocally.md
        static readonly string RedisPassword = Environment.GetEnvironmentVariable("HALIBUT_REDIS_PASSWORD") ?? "halibut-local-redis";
        CreateRedisDockerContainerForTests? redisContainer = null;
        public void OneTimeSetUp(ILogger logger)
        {
            if (!WillRunRedisTests) return;

            if (!TeamCityDetection.IsRunningInTeamCity() && TryUseAlreadyRunningRedis(logger)) return;

            redisContainer = new CreateRedisDockerContainerForTests(logger);
            redisContainer.InitializeAsync().GetAwaiter().GetResult();
            RedisTestHost.SetPort(redisContainer.container!.RedisPort);
            RedisTestHost.Password = redisContainer.container.Password;
            logger.Information("RedisPort is: {RedisPort}", RedisTestHost.Port());
        }

        static bool TryUseAlreadyRunningRedis(ILogger logger)
        {
            // Does the user already have redis running on the normal port?
            try
            {
                using var multiplexer = ConnectionMultiplexer.Connect(RedisTestHost.ConnectionString(RedisHost, RedisPortToTry, RedisPassword));
                multiplexer.GetDatabase().Ping();
            }
            catch
            {
                return false;
            }

            // We should be testing with a production-like setup, so don't test against a Redis that doesn't have a password.
            if (AcceptsConnectionsWithoutAPassword())
            {
                logger.Warning("Redis on {Host}:{Port} does not require a password, unlike Octopus Cloud, so a Redis container will be used instead. See docs/RunningRedisLocally.md for how to run a local Redis that matches Octopus Cloud", RedisHost, RedisPortToTry);
                return false;
            }

            RedisTestHost.SetPort(RedisPortToTry);
            RedisTestHost.RedisHost = RedisHost;
            RedisTestHost.Password = RedisPassword;
            logger.Information("Able to connect to redis using {Host}:{Port}", RedisHost, RedisPortToTry);
            return true;
        }

        static bool AcceptsConnectionsWithoutAPassword()
        {
            try
            {
                using var multiplexer = ConnectionMultiplexer.Connect(RedisHost + ":" + RedisPortToTry);
                multiplexer.GetDatabase().Ping();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void OneTimeTearDown(ILogger logger)
        {

            if(redisContainer != null) redisContainer.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}

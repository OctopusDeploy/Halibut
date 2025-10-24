using System;
using System.Threading.Tasks;
using Halibut.Tests.Queue.Redis.Utils;
using Serilog;

namespace Halibut.Tests.TestSetup
{
    public class CreateRedisDockerContainerForTests : IAsyncDisposable
    {
        readonly ILogger logger;
        public RedisContainer? container = null;

        public CreateRedisDockerContainerForTests(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task InitializeAsync()
        {
            logger.Information("Creating Redis container");
            container = new RedisContainerBuilder().Build();
            
            logger.Information("Starting Redis container");
            await container.StartAsync();
            logger.Information("Redis container started successfully with connection string: {ConnectionString}", container.ConnectionString);

        }
        public async ValueTask DisposeAsync()
        {
            if (container != null)
            {
                try
                {
                    await container.DisposeAsync();
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error while disposing Redis container");
                }
            }
        }
    }
}
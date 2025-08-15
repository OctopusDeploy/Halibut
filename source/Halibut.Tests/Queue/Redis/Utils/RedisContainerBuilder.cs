using System;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Halibut.Tests.Support;
using NUnit.Framework;
using Try = Halibut.Util.Try;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class RedisContainerBuilder
    {
        private string _image = "redis:7-alpine";
        private string? _customConfigPath;
        private int? _hostPort;

        /// <summary>
        /// Sets the Redis Docker image to use. Defaults to "redis:7-alpine".
        /// </summary>
        /// <param name="image">The Redis Docker image tag</param>
        /// <returns>The builder instance for method chaining</returns>
        public RedisContainerBuilder WithImage(string image)
        {
            _image = image;
            return this;
        }

        /// <summary>
        /// Sets a custom Redis configuration path to mount into the container.
        /// If not specified, uses the default redis-conf directory from the project root.
        /// </summary>
        /// <param name="configPath">The path to the Redis configuration directory</param>
        /// <returns>The builder instance for method chaining</returns>
        public RedisContainerBuilder WithCustomConfigPath(string configPath)
        {
            _customConfigPath = configPath;
            return this;
        }

        /// <summary>
        /// Sets a specific host port to bind to. If not specified, finds a free port automatically.
        /// </summary>
        /// <param name="hostPort">The host port to bind to</param>
        /// <returns>The builder instance for method chaining</returns>
        public RedisContainerBuilder WithHostPort(int hostPort)
        {
            _hostPort = hostPort;
            return this;
        }

        /// <summary>
        /// Builds and returns a configured Redis container with the specified settings.
        /// The container is not started - call StartAsync() on the returned container to start it.
        /// </summary>
        /// <returns>A configured Redis container ready to be started</returns>
        public RedisContainer Build()
        {
            var hostPort = _hostPort ?? TcpPortHelper.FindFreeTcpPort();
            var redisConfigPath = _customConfigPath ?? 
                Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../redis-conf"));

            var container = new ContainerBuilder()
                .WithImage(_image)
                .WithPortBinding(hostPort, 6379)
                .WithBindMount(redisConfigPath, "/usr/local/etc/redis")
                .WithCommand("redis-server", "/usr/local/etc/redis/redis.conf")
                .WithWaitStrategy(DotNet.Testcontainers.Builders.Wait.ForUnixContainer()
                    .UntilPortIsAvailable(6379))
                .Build();

            return new RedisContainer(container, hostPort);
        }
    }

    /// <summary>
    /// Wrapper around the testcontainers IContainer that provides Redis-specific functionality
    /// </summary>
    public class RedisContainer : IAsyncDisposable
    {
        private readonly IContainer _container;

        public RedisContainer(IContainer container, int redisPort)
        {
            _container = container;
            RedisPort = redisPort;
        }

        /// <summary>
        /// The host port that Redis is bound to
        /// </summary>
        public int RedisPort { get; }

        /// <summary>
        /// The connection string to connect to this Redis instance
        /// </summary>
        public string ConnectionString => $"localhost:{RedisPort}";

        /// <summary>
        /// Starts the Redis container
        /// </summary>
        public async Task StartAsync()
        {
            // Since I have seen errors here.
            for (int i = 0; i < 5; i++)
            {
                await Try.IgnoringError(async () => await _container.StartAsync());
            }
            await _container.StartAsync();
        }

        /// <summary>
        /// Stops the Redis container
        /// </summary>
        public Task StopAsync() => _container.StopAsync();

        /// <summary>
        /// Disposes the Redis container
        /// </summary>
        public ValueTask DisposeAsync() => _container.DisposeAsync();
    }
}
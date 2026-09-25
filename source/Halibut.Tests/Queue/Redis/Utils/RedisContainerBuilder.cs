using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Halibut.Tests.Support;
using Halibut.Tests.TestSetup.Redis;
using NUnit.Framework;
using Try = Halibut.Util.Try;

namespace Halibut.Tests.Queue.Redis.Utils
{
    /// <summary>
    /// Builds a Redis container that is as close as practical to the Redis Octopus Cloud runs for Octopus Server.
    /// See HostedScripts: source/HostedInstance/redis/redis-cache-deployment.yml
    /// </summary>
    public class RedisContainerBuilder
    {
        // Octopus Cloud runs octopusdeploy/dhi-redis (a Docker Hardened Image), which needs registry credentials to pull.
        // This is the public image of the same Redis version.
        private string _image = "redis:8.0.3";
        private string? _customConfigPath;
        private int? _hostPort;
        private string? _password;

        /// <summary>
        /// Sets the Redis Docker image to use. Defaults to "redis:8.0.3".
        /// </summary>
        /// <param name="image">The Redis Docker image tag</param>
        /// <returns>The builder instance for method chaining</returns>
        public RedisContainerBuilder WithImage(string image)
        {
            _image = image;
            return this;
        }

        /// <summary>
        /// Sets a custom directory containing the redis.conf to start Redis with.
        /// If not specified, uses the default redis-conf directory from the project root.
        /// </summary>
        /// <param name="configPath">The path to the directory containing redis.conf</param>
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
        /// Sets the password Redis requires. If not specified, a random password is generated.
        /// </summary>
        /// <param name="password">The password clients must authenticate with</param>
        /// <returns>The builder instance for method chaining</returns>
        public RedisContainerBuilder WithPassword(string password)
        {
            _password = password;
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
            var password = _password ?? Guid.NewGuid().ToString("N");
            var redisConfigPath = _customConfigPath ??
                Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../redis-conf"));

            // Octopus Cloud mounts redis.conf as a file, we pass its settings as arguments instead. A bind mount keeps the host's
            // permissions, so Redis (running as 1001) would fail to start if the checkout is not readable by other users.
            var redisConfigArguments = RedisConfigFileToArguments(Path.Combine(redisConfigPath, "redis.conf"));

            var container = new ContainerBuilder()
                .WithImage(_image)
                .WithPortBinding(hostPort, 6379)
                // Start redis-server directly, the official image's entrypoint loads the Redis 8 modules (JSON, search etc.) which Octopus Cloud's image does not.
                .WithEntrypoint("redis-server")
                .WithCommand(redisConfigArguments.Concat(new[] { "--requirepass", password }).ToArray())
                // Octopus Cloud runs Redis as a non-root user, with a read-only root filesystem, no capabilities and in-memory /data and /tmp.
                .WithTmpfsMount("/data")
                .WithTmpfsMount("/tmp")
                .WithCreateParameterModifier(parameters =>
                {
                    parameters.User = "1001:1001";
                    parameters.HostConfig.ReadonlyRootfs = true;
                    parameters.HostConfig.CapDrop = new List<string> { "ALL" };
                    parameters.HostConfig.SecurityOpt = new List<string> { "no-new-privileges" };
                })
                // The same check as the readiness probe in Octopus Cloud.
                .WithWaitStrategy(DotNet.Testcontainers.Builders.Wait.ForUnixContainer()
                    .UntilCommandIsCompleted("bash", "-ec", $"[ \"$(REDISCLI_AUTH={password} redis-cli -h localhost ping)\" = PONG ]"))
                .Build();

            return new RedisContainer(container, hostPort, password);
        }

        /// <summary>
        /// Converts each directive in a redis.conf file into redis-server arguments, e.g. `save ""` becomes `--save` and ``.
        /// </summary>
        static string[] RedisConfigFileToArguments(string redisConfigFile)
        {
            return File.ReadAllLines(redisConfigFile)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#"))
                .SelectMany(line =>
                {
                    var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    return new[] { "--" + parts[0] }.Concat(parts.Skip(1).Select(value => value.Trim('"')));
                })
                .ToArray();
        }
    }

    /// <summary>
    /// Wrapper around the testcontainers IContainer that provides Redis-specific functionality
    /// </summary>
    public class RedisContainer : IAsyncDisposable
    {
        private readonly IContainer _container;

        public RedisContainer(IContainer container, int redisPort, string password)
        {
            _container = container;
            RedisPort = redisPort;
            Password = password;
        }

        /// <summary>
        /// The host port that Redis is bound to
        /// </summary>
        public int RedisPort { get; }

        /// <summary>
        /// The password Redis requires
        /// </summary>
        public string Password { get; }

        /// <summary>
        /// The connection string to connect to this Redis instance
        /// </summary>
        public string ConnectionString => RedisTestHost.ConnectionString("localhost", RedisPort, Password);

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
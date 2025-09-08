using System;
using Halibut.Diagnostics;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestSetup.Redis;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class RedisFacadeBuilder
    {
        public static RedisFacade CreateRedisFacade(string? host = null, int? port = 0, Guid? prefix = null)
        {
            port = port == 0 ? RedisTestHost.Port() : port; 
            return new RedisFacade((host??RedisTestHost.RedisHost) + ":" + port, (prefix ?? Guid.NewGuid()).ToString(), new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));
        }

        public static RedisFacade CreateRedisFacade(PortForwarder portForwarder, Guid? prefix = null)
        {
            return CreateRedisFacade(host: portForwarder.PublicEndpoint.Host,
                port: portForwarder.ListeningPort,
                prefix: prefix);
        }
    }
}
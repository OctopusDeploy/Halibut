using System;
using Halibut.Logging;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestSetup.Redis;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class RedisFacadeBuilder
    {
        public static RedisFacade CreateRedisFacade(string? host = null, int? port = 0, Guid? prefix = null, IRedisFacadeObserver? redisFacadeObserver = null, string? password = null)
        {
            port = port == 0 ? RedisTestHost.Port() : port;
            var connectionString = RedisTestHost.ConnectionString(host ?? RedisTestHost.RedisHost, port!.Value, password ?? RedisTestHost.Password);
            return new RedisFacade(connectionString, (prefix ?? Guid.NewGuid()).ToString(), new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""), redisFacadeObserver);
        }

        public static RedisFacade CreateRedisFacade(PortForwarder portForwarder, Guid? prefix = null, IRedisFacadeObserver? redisFacadeObserver = null)
        {
            return CreateRedisFacade(host: portForwarder.PublicEndpoint.Host,
                port: portForwarder.ListeningPort,
                prefix: prefix,
                redisFacadeObserver: redisFacadeObserver);
        }
    }
}

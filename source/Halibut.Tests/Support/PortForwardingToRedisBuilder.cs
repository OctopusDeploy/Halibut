using System;
using Halibut.Tests.TestSetup.Redis;
using Octopus.TestPortForwarder;
using Serilog;

namespace Halibut.Tests.Support
{
    public static class PortForwardingToRedisBuilder
    {
        public static PortForwarder ForwardingToRedis(ILogger logger)
        {
            return new PortForwarderBuilder(new Uri("http://" + RedisTestHost.RedisHost + ":" + RedisTestHost.Port()), logger).Build();
        }
    }
}

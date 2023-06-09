using Octopus.TestPortForwarder;

namespace Halibut.Tests.Util
{
    public static class PortForwarderUtil
    {
        public static PortForwarderBuilder ForwardingToLocalPort(int port)
        {
            return PortForwarderBuilder.ForwardingToLocalPort(port, new SerilogLoggerBuilder().Build());
        }
    }
}
using System;
using Halibut.Tests.Support.BackwardsCompatibility;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public static class ClientAndServiceBuilderExtensionMethods
    {
        public static LatestClientAndLatestServiceBuilder AsLatestClientAndLatestServiceBuilder(this IClientAndServiceBuilder clientAndServiceBuilder)
        {
            return (LatestClientAndLatestServiceBuilder) clientAndServiceBuilder;
        }
        
        public static PreviousClientVersionAndLatestServiceBuilder AsPreviousClientVersionAndLatestServiceBuilder(this IClientAndServiceBuilder clientAndServiceBuilder)
        {
            return (PreviousClientVersionAndLatestServiceBuilder) clientAndServiceBuilder;
        }

        public static IClientAndServiceBuilder WithAsyncService<TContract, TClientContract>(this IClientAndServiceBuilder clientAndServiceBuilder, Func<TClientContract> implementation)
        {
            if (clientAndServiceBuilder is LatestClientAndLatestServiceBuilder)
            {
                return clientAndServiceBuilder.AsLatestClientAndLatestServiceBuilder().WithAsyncService<TContract, TClientContract>(implementation);
            }

            return clientAndServiceBuilder.AsPreviousClientVersionAndLatestServiceBuilder().WithAsyncService<TContract, TClientContract>(implementation);
        }

        public static T WithPortForwarding<T>(this T portForwarderBuilder) where T : IClientAndServiceBuilder
        {
            portForwarderBuilder.WithPortForwarding(out _, CreateDefaultPortForwarder);
            return portForwarderBuilder;
        }

        public static T WithPortForwarding<T>(this T portForwarderBuilder, Func<int, PortForwarder> portForwarderFactory) where T : IClientAndServiceBuilder
        {
            portForwarderBuilder.WithPortForwarding(out _, portForwarderFactory);
            return portForwarderBuilder;
        }

        public static T WithPortForwarding<T>(this T portForwarderBuilder, out Reference<PortForwarder> portForwarder) where T : IClientAndServiceBuilder
        {
            portForwarderBuilder.WithPortForwarding(out portForwarder, CreateDefaultPortForwarder);
            return portForwarderBuilder;
        }

        static PortForwarder CreateDefaultPortForwarder(int port)
        {
            return PortForwarderUtil.ForwardingToLocalPort(port).Build();
        }
    }
}
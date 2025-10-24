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

        public static IClientAndServiceBuilder OnLatestClientAndLatestServiceBuilder(this IClientAndServiceBuilder clientAndServiceBuilder, Action<LatestClientAndLatestServiceBuilder> configure)
        {
            if (clientAndServiceBuilder is LatestClientAndLatestServiceBuilder latestClientAndLatestServiceBuilder) configure(latestClientAndLatestServiceBuilder);
            return clientAndServiceBuilder;
        }

        public static PreviousClientVersionAndLatestServiceBuilder AsPreviousClientVersionAndLatestServiceBuilder(this IClientAndServiceBuilder clientAndServiceBuilder)
        {
            return (PreviousClientVersionAndLatestServiceBuilder) clientAndServiceBuilder;
        }

        public static LatestClientAndPreviousServiceVersionBuilder AsLatestClientAndPreviousServiceVersionBuilder(this IClientAndServiceBuilder clientAndServiceBuilder)
        {
            return (LatestClientAndPreviousServiceVersionBuilder) clientAndServiceBuilder;
        }

        public static IClientAndServiceBuilder WithAsyncService<TContract, TClientContract>(this IClientAndServiceBuilder clientAndServiceBuilder, Func<TClientContract> implementation)
        {
            if (clientAndServiceBuilder is LatestClientAndLatestServiceBuilder)
            {
                return clientAndServiceBuilder.AsLatestClientAndLatestServiceBuilder().WithAsyncService<TContract, TClientContract>(implementation);
            }

            return clientAndServiceBuilder.AsPreviousClientVersionAndLatestServiceBuilder().WithAsyncService<TContract, TClientContract>(implementation);
        }

        public static T WithPortForwarding<T>(this T builder) where T : IClientAndServiceBuilder
        {
            builder.WithPortForwarding(out _, CreateDefaultPortForwarder);
            return builder;
        }

        public static T WithPortForwarding<T>(this T builder, Func<int, PortForwarder> portForwarderFactory) where T : IClientAndServiceBuilder
        {
            builder.WithPortForwarding(out _, portForwarderFactory);
            return builder;
        }

        public static T WithPortForwarding<T>(this T builder, out Reference<PortForwarder> portForwarder) where T : IClientAndServiceBuilder
        {
            builder.WithPortForwarding(out portForwarder, CreateDefaultPortForwarder);
            return builder;
        }

        public static T WithProxy<T>(this T builder) where T : IClientAndServiceBuilder
        {
            builder.WithProxy(out _);
            return builder;
        }

        static PortForwarder CreateDefaultPortForwarder(int port)
        {
            return PortForwarderUtil.ForwardingToLocalPort(port).Build();
        }
    }
}
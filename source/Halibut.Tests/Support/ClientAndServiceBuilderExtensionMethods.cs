using System;
using Halibut.Tests.Support.BackwardsCompatibility;

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
    }
}
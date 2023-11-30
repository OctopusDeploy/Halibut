using System;

namespace Halibut.Tests.Support
{
    public static class ClientBuilderExtensionMethods
    {
        public static LatestClientBuilder AsLatestClientBuilder(this IClientOnlyBuilder clientBuilder)
        {
            return (LatestClientBuilder) clientBuilder;
        }
    }
}
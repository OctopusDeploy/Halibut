using System;

namespace Halibut.Tests.Support
{
    public static class ClientBuilderExtensionMethods
    {
        public static LatestClientBuilder AsLatestClientBuilder(this IClientBuilder clientBuilder)
        {
            return (LatestClientBuilder) clientBuilder;
        }
    }
}
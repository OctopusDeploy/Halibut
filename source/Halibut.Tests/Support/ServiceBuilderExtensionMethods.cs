using System;

namespace Halibut.Tests.Support
{
    public static class ServiceBuilderExtensionMethods
    {
        public static LatestServiceBuilder AsLatestServiceBuilder(this IServiceOnlyBuilder serviceOnlyBuilder)
        {
            return (LatestServiceBuilder) serviceOnlyBuilder;
        }
    }
}
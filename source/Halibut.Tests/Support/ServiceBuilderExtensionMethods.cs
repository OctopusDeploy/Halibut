using System;

namespace Halibut.Tests.Support
{
    public static class ServiceBuilderExtensionMethods
    {
        public static LatestServiceBuilder AsLatestServiceBuilder(this IServiceBuilder serviceBuilder)
        {
            return (LatestServiceBuilder) serviceBuilder;
        }
    }
}
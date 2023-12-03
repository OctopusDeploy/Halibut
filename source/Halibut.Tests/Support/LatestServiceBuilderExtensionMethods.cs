using System;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.Support
{
    static class LatestServiceBuilderExtensionMethods
    {
        public static LatestServiceBuilder WithCountingService(this LatestServiceBuilder builder, IAsyncCountingService countingService)
        {
            return builder.WithAsyncService<ICountingService, IAsyncCountingService>(() => countingService);
        }
    }
}

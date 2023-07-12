using System;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;
using ICachingService = Halibut.TestUtils.Contracts.ICachingService;

namespace Halibut.Tests.Support
{
    static class LatestClientAndLatestServiceBuilderExtensionMethods
    {
        public static LatestClientAndLatestServiceBuilder WithEchoService(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithService<IEchoService>(() => new EchoService());
        }

        public static LatestClientAndLatestServiceBuilder WithMultipleParametersTestService(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithService<IMultipleParametersTestService>(() => new MultipleParametersTestService());
        }

        public static LatestClientAndLatestServiceBuilder WithDoSomeActionService(this LatestClientAndLatestServiceBuilder builder, Action action)
        {
            return builder.WithService<IDoSomeActionService>(() => new DoSomeActionService(action));
        }

        public static LatestClientAndLatestServiceBuilder WithReadDataStreamService(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithService<IReadDataStreamService>(() => new ReadDataStreamService());
        }

        public static LatestClientAndLatestServiceBuilder WithCachingService(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithService<ICachingService>(() => new CachingService());
        }
    }
}

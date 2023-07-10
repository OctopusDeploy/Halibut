using System;
using Halibut.Tests.TestServices;

namespace Halibut.Tests.Support
{
    static class IClientServiceBuilderExtensionMethods
    {
        public static IClientAndServiceBuilder WithStandardServices(this IClientAndServiceBuilder builder)
        {
            return builder.WithEchoService().WithCachingService().WithMultipleParametersTestService();
        }
        
        public static IClientAndServiceBuilder WithEchoService(this IClientAndServiceBuilder builder)
        {
            return builder.WithService<IEchoService>(() => new EchoService());
        }

        public static IClientAndServiceBuilder WithMultipleParametersTestService(this IClientAndServiceBuilder builder)
        {
            return builder.WithService<IMultipleParametersTestService>(() => new MultipleParametersTestService());
        }

        public static IClientAndServiceBuilder WithDoSomeActionService(this IClientAndServiceBuilder builder, Action action)
        {
            return builder.WithService<IDoSomeActionService>(() => new DoSomeActionService(action));
        }

        public static IClientAndServiceBuilder WithReadDataStreamService(this IClientAndServiceBuilder builder)
        {
            return builder.WithService<IReadDataStreamService>(() => new ReadDataStreamService());
        }

        public static IClientAndServiceBuilder WithCachingService(this IClientAndServiceBuilder builder)
        {
            return builder.WithService<ICachingService>(() => new CachingService());
        }
    }
}

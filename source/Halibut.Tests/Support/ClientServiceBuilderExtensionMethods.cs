using System;
using Halibut.Tests.TestServices;

namespace Halibut.Tests.Support
{
    static class ClientServiceBuilderExtensionMethods
    {
        public static ClientServiceBuilder WithEchoService(this ClientServiceBuilder builder)
        {
            return builder.WithService<IEchoService>(() => new EchoService());
        }

        public static ClientServiceBuilder WithMultipleParametersTestService(this ClientServiceBuilder builder)
        {
            return builder.WithService<IMultipleParametersTestService>(() => new MultipleParametersTestService());
        }

        public static ClientServiceBuilder WithDoSomeActionService(this ClientServiceBuilder builder, Action action)
        {
            return builder.WithService<IDoSomeActionService>(() => new DoSomeActionService(action));
        }

        public static ClientServiceBuilder WithReadDataStreamService(this ClientServiceBuilder builder)
        {
            return builder.WithService<IReadDataStreamService>(() => new ReadDataStreamService());
        }

        public static ClientServiceBuilder WithCachingService(this ClientServiceBuilder builder)
        {
            return builder.WithService<ICachingService>(() => new CachingService());
        }
    }
}

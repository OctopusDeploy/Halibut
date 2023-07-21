using System;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
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

        public static LatestClientAndLatestServiceBuilder WithComplexObjectService(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithService<IComplexObjectService>(() => new ComplexObjectService());
        }
        
        public static LatestClientAndLatestServiceBuilder WithLockService(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithService<ILockService>(() => new LockService());
        }
        
        public static LatestClientAndLatestServiceBuilder WithCountingService(this LatestClientAndLatestServiceBuilder builder)
        {
            var singleCountingService = new CountingService();
            return builder.WithService<ICountingService>(() => singleCountingService);
        }

        public static LatestClientAndLatestServiceBuilder WithDoSomeActionService(this LatestClientAndLatestServiceBuilder builder, Action action)
        {
            return builder.WithService<IDoSomeActionService>(() => new DoSomeActionService(action));
        }
        
        public static LatestClientAndLatestServiceBuilder WithReturnSomeDataStreamService(this LatestClientAndLatestServiceBuilder builder, Func<DataStream> dataStreamCreator)
        {
            return builder.WithService<IReturnSomeDataStreamService>(() => new ReturnSomeDataStreamService(dataStreamCreator));
        }

        public static LatestClientAndLatestServiceBuilder WithReadDataStreamService(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithService<IReadDataStreamService>(() => new ReadDataStreamService());
        }

        public static LatestClientAndLatestServiceBuilder WithInstantReconnectPollingRetryPolicy(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithPollingReconnectRetryPolicy(() => new RetryPolicy(1, TimeSpan.Zero, TimeSpan.Zero));
        }
    }
}

using System;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;
using Halibut.Util;

namespace Halibut.Tests.Support
{
    static class LatestServiceBuilderExtensionMethods
    {
        public static LatestServiceBuilder WithEchoService(this LatestServiceBuilder builder)
        {
            return builder.WithAsyncService<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());
        }

        public static LatestServiceBuilder WithMultipleParametersTestService(this LatestServiceBuilder builder)
        {
            return builder.WithAsyncService<IMultipleParametersTestService, IAsyncMultipleParametersTestService>(() => new AsyncMultipleParametersTestService());
        }

        public static LatestServiceBuilder WithComplexObjectService(this LatestServiceBuilder builder)
        {
            return builder.WithAsyncService<IComplexObjectService, IAsyncComplexObjectService>(() => new AsyncComplexObjectService());
        }
        
        public static LatestServiceBuilder WithLockService(this LatestServiceBuilder builder)
        {
            return builder.WithAsyncService<ILockService, IAsyncLockService>(() => new AsyncLockService());
        }
        
        public static LatestServiceBuilder WithCountingService(this LatestServiceBuilder builder)
        {
            var singleCountingService = new AsyncCountingService();
            return builder.WithAsyncService<ICountingService, IAsyncCountingService>(() => singleCountingService);
        }
        
        public static LatestServiceBuilder WithCountingService(this LatestServiceBuilder builder, IAsyncCountingService countingService)
        {
            return builder.WithAsyncService<ICountingService, IAsyncCountingService>(() => countingService);
        }

        public static LatestServiceBuilder WithDoSomeActionService(this LatestServiceBuilder builder, Action action)
        {
            return builder.WithAsyncService<IDoSomeActionService, IAsyncDoSomeActionService>(() => new AsyncDoSomeActionService(action));
        }
        
        public static LatestServiceBuilder WithReturnSomeDataStreamService(this LatestServiceBuilder builder, Func<DataStream> dataStreamCreator)
        {
            return builder.WithAsyncService<IReturnSomeDataStreamService, IAsyncReturnSomeDataStreamService>(() => new AsyncReturnSomeDataStreamService(dataStreamCreator));
        }

        public static LatestServiceBuilder WithReadDataStreamService(this LatestServiceBuilder builder)
        {
            return builder.WithAsyncService<IReadDataStreamService, IAsyncReadDataStreamService>(() => new AsyncReadDataStreamService());
        }

        public static LatestServiceBuilder WithInstantReconnectPollingRetryPolicy(this LatestServiceBuilder builder)
        {
            return builder.WithPollingReconnectRetryPolicy(() => new RetryPolicy(1, TimeSpan.Zero, TimeSpan.Zero));
        }

        public static LatestServiceBuilder WhenTestingAsyncClient(this LatestServiceBuilder builder, ClientAndServiceTestCase clientAndServiceTestCase, Action<LatestServiceBuilder> action)
        {

            action(builder);
            return builder;
        }
    }
}

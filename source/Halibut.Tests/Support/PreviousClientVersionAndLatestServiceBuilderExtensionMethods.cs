using System;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.Support
{
    static class PreviousClientVersionAndLatestServiceBuilderExtensionMethods
    {
        public static PreviousClientVersionAndLatestServiceBuilder WithEchoService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            return builder.WithAsyncService<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());
        }

        public static PreviousClientVersionAndLatestServiceBuilder WithMultipleParametersTestService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            return builder.WithAsyncService<IMultipleParametersTestService, IAsyncMultipleParametersTestService>(() => new AsyncMultipleParametersTestService());
        }

        public static PreviousClientVersionAndLatestServiceBuilder WithComplexObjectService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            return builder.WithAsyncService<IComplexObjectService, IAsyncComplexObjectService>(() => new AsyncComplexObjectService());
        }
        
        public static PreviousClientVersionAndLatestServiceBuilder WithLockService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            return builder.WithAsyncService<ILockService, IAsyncLockService>(() => new AsyncLockService());
        }
        
        public static PreviousClientVersionAndLatestServiceBuilder WithCountingService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            var singleCountingService = new AsyncCountingService();
            return builder.WithAsyncService<ICountingService, IAsyncCountingService>(() => singleCountingService);
        }

        public static PreviousClientVersionAndLatestServiceBuilder WithReadDataStreamService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            return builder.WithAsyncService<IReadDataStreamService, IAsyncReadDataStreamService>(() => new AsyncReadDataStreamService());
        }
    }
}

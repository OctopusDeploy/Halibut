using System;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Observability;
using Halibut.Util;
using ICachingService = Halibut.TestUtils.Contracts.ICachingService;

namespace Halibut.Tests.Support
{
    static class PreviousClientVersionAndLatestServiceBuilderExtensionMethods
    {
        public static PreviousClientVersionAndLatestServiceBuilder WithEchoService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            return builder.WithService<IEchoService>(() => new EchoService());
        }

        public static PreviousClientVersionAndLatestServiceBuilder WithMultipleParametersTestService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            return builder.WithService<IMultipleParametersTestService>(() => new MultipleParametersTestService());
        }

        public static PreviousClientVersionAndLatestServiceBuilder WithComplexObjectService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            return builder.WithService<IComplexObjectService>(() => new ComplexObjectService());
        }
        
        public static PreviousClientVersionAndLatestServiceBuilder WithLockService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            return builder.WithService<ILockService>(() => new LockService());
        }
        
        public static PreviousClientVersionAndLatestServiceBuilder WithCountingService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            var singleCountingService = new CountingService();
            return builder.WithService<ICountingService>(() => singleCountingService);
        }
        
        public static PreviousClientVersionAndLatestServiceBuilder WithCountingService(this PreviousClientVersionAndLatestServiceBuilder builder, ICountingService countingService)
        {
            return builder.WithService<ICountingService>(() => countingService);
        }
        
        public static PreviousClientVersionAndLatestServiceBuilder WithReadDataStreamService(this PreviousClientVersionAndLatestServiceBuilder builder)
        {
            return builder.WithService<IReadDataStreamService>(() => new ReadDataStreamService());
        }
    }
}

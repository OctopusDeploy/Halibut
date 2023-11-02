﻿using System;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Observability;
using Halibut.Util;

namespace Halibut.Tests.Support
{
    static class LatestClientAndLatestServiceBuilderExtensionMethods
    {
        public static LatestClientAndLatestServiceBuilder WithEchoService(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithAsyncService<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());
        }

        public static LatestClientAndLatestServiceBuilder WithMultipleParametersTestService(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithService<IMultipleParametersTestService>(() => new MultipleParametersTestService());
        }

        public static LatestClientAndLatestServiceBuilder WithComplexObjectService(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithAsyncService<IComplexObjectService, IAsyncComplexObjectService>(() => new AsyncComplexObjectService());
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
        
        public static LatestClientAndLatestServiceBuilder WithCountingService(this LatestClientAndLatestServiceBuilder builder, ICountingService countingService)
        {
            return builder.WithService<ICountingService>(() => countingService);
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
            return builder.WithAsyncService<IReadDataStreamService, IAsyncReadDataStreamService>(() => new AsyncReadDataStreamService());
        }

        public static LatestClientAndLatestServiceBuilder WithInstantReconnectPollingRetryPolicy(this LatestClientAndLatestServiceBuilder builder)
        {
            return builder.WithPollingReconnectRetryPolicy(() => new RetryPolicy(1, TimeSpan.Zero, TimeSpan.Zero));
        }

        public static LatestClientAndLatestServiceBuilder WhenTestingAsyncClient(this LatestClientAndLatestServiceBuilder builder, ClientAndServiceTestCase clientAndServiceTestCase, Action<LatestClientAndLatestServiceBuilder> action)
        {

            action(builder);
            return builder;
        }
        
        public static LatestClientAndLatestServiceBuilder WithConnectionObserverOnTcpServer(this LatestClientAndLatestServiceBuilder builder, IConnectionsObserver connectionsObserver)
        {
            if (builder.ServiceConnectionType == ServiceConnectionType.Listening)
            {
                return builder.WithServiceConnectionsObserver(connectionsObserver);
            }

            return builder.WithClientConnectionsObserver(connectionsObserver);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCallingAMethodThatDoesNotExist : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task AMethodNotFoundHalibutClientExceptionShouldBeRaisedByTheClient(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var services = new SingleServiceFactory(new object(), typeof(IEchoService));

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithServiceFactory(services)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                
                await AssertException.Throws<MethodNotFoundHalibutClientException>(() => echo.SayHelloAsync("Say hello to a service that does not exist."));
            }
        }

        public class SingleServiceFactory : IServiceFactory
        {
            readonly object Service;
            readonly Type serviceType;

            public SingleServiceFactory(object service, Type serviceType)
            {
                Service = service;
                this.serviceType = serviceType;
            }

            public IServiceLease CreateService(string serviceName)
            {
                return new SharedNeverExpiringLease(Service);
            }

            public IReadOnlyList<Type> RegisteredServiceTypes
            {
                get => new[] { serviceType };
            }
        }

        public class SharedNeverExpiringLease : IServiceLease
        {
            public SharedNeverExpiringLease(object service)
            {
                Service = service;
            }

            public void Dispose()
            {
            }

            public object Service { get; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCallingAMethodThatDoesNotExist
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task AMethodNotFoundHalibutClientExceptionShouldBeRaisedByTheClient(ServiceConnectionType serviceConnectionType)
        {
            var services = new SingleServiceFactory(new object(), typeof(EchoService));

            using (var clientAndService = ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithServiceFactory(services)
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();

                Func<string> readAsyncCall = () => echo.SayHello("Say hello to a service that does not exist.");

                readAsyncCall.Should().Throw<MethodNotFoundHalibutClientException>();
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
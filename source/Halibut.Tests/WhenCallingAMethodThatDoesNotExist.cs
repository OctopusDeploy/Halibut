using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    public static class WhenCallingAMethodThatDoesNotExist
    {
        public class OnAPollingService
        {
            [Test]
            public async Task AMethodNotFoundHalibutClientExceptionShouldBeRaisedByTheClient()
            {
                var services = new SingleServiceFactory(new object(), typeof(EchoService));
                
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                {
                    var octopusPort = octopus.Listen();
                    using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                    {
                        octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                        tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                        var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                        var readAsyncCall = () => echo.SayHello("Say hello to a service that does not exist.");

                        readAsyncCall.Should().Throw<MethodNotFoundHalibutClientException>();
                    }
                }
            }
        }

        public class OnAListeningTentacle
        {
            [Test]
            public async Task AMethodNotFoundHalibutClientExceptionShouldBeRaisedByTheClient()
            {
                var services = new SingleServiceFactory(new object(), typeof(EchoService));
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
                {
                    var tentaclePort = tentacleListening.Listen();
                    tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                    var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                    var readAsyncCall = () => echo.SayHello("Say hello to a service that does not exist.");

                    readAsyncCall.Should().Throw<MethodNotFoundHalibutClientException>();
                }
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
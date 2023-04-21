using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    public static class WhenCallingAServiceThatDoesNotExist
    {
        public class OnAPollingService
        {
            [Test]
            public async Task AServiceNotFoundHalibutClientExceptionShouldBeRaisedByTheClient()
            {
                var services = new DelegateServiceFactory();
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                {
                    var octopusPort = octopus.Listen();
                    using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                    {
                        octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                        tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                        var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                        Func<string> readAsyncCall = () => echo.SayHello("Say hello to a service that does not exist.");

                        readAsyncCall.Should().Throw<ServiceNotFoundHalibutClientException>();
                    }
                }
            }
        }

        public class OnAListeningTentacle
        {
            [Test]
            public async Task AServiceNotFoundHalibutClientExceptionShouldBeRaisedByTheClient()
            {
                var services = new DelegateServiceFactory();
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
                {
                    var tentaclePort = tentacleListening.Listen();
                    tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                    var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                    Func<string> readAsyncCall = () => echo.SayHello("Say hello to a service that does not exist.");

                    readAsyncCall.Should().Throw<ServiceNotFoundHalibutClientException>();
                }
            }
        }
    }
}
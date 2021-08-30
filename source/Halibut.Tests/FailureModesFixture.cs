using System;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class FailureModesFixture
    {
        static DelegateServiceFactory GetStubDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            return services;
        }

        [Test]
        public void FailsWhenSendingToPollingMachineButNothingPicksItUp()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var endpoint = new ServiceEndPoint("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint)
                {
                    TcpClientConnectTimeout = TimeSpan.FromSeconds(1),
                    PollingRequestQueueTimeout = TimeSpan.FromSeconds(5)
                };
                var echo = octopus.CreateClient<IEchoService>(endpoint);
                var error = Assert.Throws<HalibutClientException>(() => echo.SayHello("Paul"));
                error.Message.Should().Contain("the polling endpoint did not collect the request within the allowed time");
            }
        }

        [Test]
        public void FailWhenServerThrowsAnException()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                ex.Message.Should().Contain("at Halibut.Tests.TestServices.EchoService.Crash()").And.Contain("divide by zero");
            }
        }

        [Test]
        public void FailWhenServerThrowsAnExceptionOnPolling()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                ex.Message.Should().Contain("at Halibut.Tests.TestServices.EchoService.Crash()").And.Contain("divide by zero");
            }
        }

        [Test]
        public void FailOnInvalidHostname()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000", Certificates.TentacleListeningPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                var message = ex.Message;

                message.Should().Contain("when sending a request to 'https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000/', before the request");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    message.Should().Contain("No such host is known");
                }
                else
                {
                    // Failed with: An error occurred when sending a request to 'https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000/', before the request could begin: Name or service not known, but found False.
                    new [] {"No such device or address", "Resource temporarily unavailable", "Name or service not known"}.Any(message.Contains).Should().BeTrue($"Message does not match known strings: {message}");
                }
            }
        }

        [Test]
        public void FailOnInvalidPort()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var endpoint = new ServiceEndPoint("https://google.com:88", Certificates.TentacleListeningPublicThumbprint)
                {
                    TcpClientConnectTimeout = TimeSpan.FromSeconds(2),
                    RetryCountLimit = 2
                };
                var echo = octopus.CreateClient<IEchoService>(endpoint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                ex.Message.Should().Be("An error occurred when sending a request to 'https://google.com:88/', before the request could begin: The client was unable to establish the initial connection within the timeout 00:00:02.");
            }
        }

        [Test]
        public void FailWhenListeningClientPresentsWrongCertificate()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.TentaclePolling))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                Assert.Throws<HalibutClientException>(() => echo.SayHello("World"));
            }
        }

        [Test]
        public void FailWhenListeningServerPresentsWrongCertificate()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentaclePollingPublicThumbprint);

                Assert.Throws<HalibutClientException>(() => echo.SayHello("World"));
            }
        }
    }
}
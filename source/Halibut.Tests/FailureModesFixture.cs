using System;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class FailureModesFixture
    {
        DelegateServiceFactory services;

        [SetUp]
        public void SetUp()
        {
            services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
        }

        [Test]
        public void FailsWhenSendingToPollingMachineButNothingPicksItUp()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                var error = Assert.Throws<HalibutClientException>(() => echo.SayHello("Paul"));
                Assert.That(error.Message, Is.StringContaining("the polling endpoint did not collect the request within the allowed time"));
            }
        }

        [Test]
        public void FailWhenServerThrowsAnException()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                Assert.That(ex.Message, Is.StringContaining("at Halibut.Tests.TestServices.EchoService.Crash()").And.StringContaining("divide by zero"));
            }
        }

        [Test]
        public void FailWhenServerThrowsAnExceptionOnPolling()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                Assert.That(ex.Message, Is.StringContaining("at Halibut.Tests.TestServices.EchoService.Crash()").And.StringContaining("divide by zero"));
            }
        }

        [Test]
        public void FailOnInvalidHostname()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000", Certificates.TentacleListeningPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                Assert.That(ex.Message, Is.StringContaining("when sending a request to 'https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000/', before the request").And.StringContaining("No such host is known"));
            }
        }

        [Test]
        public void FailOnInvalidPort()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("https://google.com:88", Certificates.TentacleListeningPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                Assert.That(ex.Message, Is.StringContaining("when sending a request to 'https://google.com:88/', before the request").And.StringContaining("unable to establish the initial connection "));
            }
        }

        [Test]
        public void FailWhenListeningClientPresentsWrongCertificate()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.TentaclePolling))
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
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentaclePollingPublicThumbprint);

                var ex = Assert.Throws<HalibutClientException>(() => echo.SayHello("World"));
            }
        }
    }
}
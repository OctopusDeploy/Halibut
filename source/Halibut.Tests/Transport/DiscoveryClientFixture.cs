using System;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.TestServices;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class DiscoveryClientFixture
    {
        ServiceEndPoint endpoint;
        HalibutRuntime tentacle;

        [SetUp]
        public void SetUp()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            tentacle = new HalibutRuntime(services, Certificates.TentacleListening);
            var tentaclePort = tentacle.Listen();
            tentacle.Trust(Certificates.OctopusPublicThumbprint);
            endpoint = new ServiceEndPoint("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint)
            {
                ConnectionErrorRetryTimeout = TimeSpan.MaxValue
            };
        }

        [TearDown]
        public void TearDown()
        {
            tentacle.Dispose();
        }

        [Test]
        public void DiscoverMethodReturnsEndpointDetails()
        {
            var client = new DiscoveryClient();
            var discovered = client.Discover(new ServiceEndPoint(endpoint.BaseUri, ""));

            discovered.RemoteThumbprint.Should().BeEquivalentTo(endpoint.RemoteThumbprint);
            discovered.BaseUri.Should().BeEquivalentTo(endpoint.BaseUri);
        }

        [Test]
        public void DiscoveringNonExistantEndpointThrows()
        {
            var client = new DiscoveryClient();
            var fakeEndpoint = new ServiceEndPoint("https://fake-tentacle.example", "");

            Assert.Throws<HalibutClientException>(() => client.Discover(fakeEndpoint), "No such host is known");
        }
    }
}
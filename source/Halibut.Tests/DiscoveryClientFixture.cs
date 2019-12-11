using System;
using System.Threading;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class DiscoveryClientFixture : IDisposable
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

        public void Dispose()
        {
            tentacle.Dispose();
        }


        [Test]
        public void DiscoverMethodReturnsEndpointDetails()
        {
            var client = new DiscoveryClient();
            var discovered = client.Discover(new ServiceEndPoint(endpoint.BaseUri, ""), CancellationToken.None);

            discovered.RemoteThumbprint.ShouldBeEquivalentTo(endpoint.RemoteThumbprint);
            discovered.BaseUri.ShouldBeEquivalentTo(endpoint.BaseUri);
        }

        [Test]
        public void DiscoveringNonExistantEndpointThrows()
        {
            var client = new DiscoveryClient();
            var fakeEndpoint = new ServiceEndPoint("https://fake-tentacle.example", "");

            Assert.Throws<HalibutClientException>(() => client.Discover(fakeEndpoint, CancellationToken.None), "No such host is known");
        }
    }
}
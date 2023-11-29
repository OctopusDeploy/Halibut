using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Transport;
using Halibut.Transport.Streams;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Transport
{
    public class DiscoveryClientFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false)]
        public async Task DiscoverMethodReturnsEndpointDetails(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .Build(CancellationToken);

            var client = new DiscoveryClient(new StreamFactory());
            var discovered = await client.DiscoverAsync(
                new ServiceEndPoint(clientAndService.GetServiceEndPoint().BaseUri, "", clientAndService.Client!.TimeoutsAndLimits), 
                clientAndService.Client.TimeoutsAndLimits, 
                CancellationToken);

            discovered.RemoteThumbprint.Should().BeEquivalentTo(clientAndService.GetServiceEndPoint().RemoteThumbprint);
            discovered.BaseUri.Should().BeEquivalentTo(clientAndService.GetServiceEndPoint().BaseUri);
        }

        [Test]
        public async Task DiscoveringNonExistentEndpointThrows()
        {
            var client = new DiscoveryClient(new StreamFactory());
            var fakeEndpoint = new ServiceEndPoint("https://fake-tentacle.example", "", new HalibutTimeoutsAndLimitsForTestsBuilder().Build());

            await AssertAsync.Throws<HalibutClientException>(() => client.DiscoverAsync(fakeEndpoint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build(), CancellationToken), "No such host is known");
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling:false)]
        public async Task OctopusCanDiscoverTentacle(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .Build(CancellationToken))
            {
                var info = await clientAndService.Client!.DiscoverAsync(clientAndService.ServiceUri, CancellationToken);
                    
                info.RemoteThumbprint.Should().Be(Certificates.TentacleListeningPublicThumbprint);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false)]
        public async Task DiscoverShouldRespectTcpClientReceiveTimeout(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var dataTransferObserverPauser = new DataTransferObserverBuilder()
                .WithWritePausing(Logger, 1)
                .Build();
            var dataTransferObserverDoNothing = new DataTransferObserverBuilder().Build();

            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port)
                    .WithDataObserver(() => new BiDirectionalDataTransferObserver(dataTransferObserverDoNothing, dataTransferObserverPauser))
                    .Build())
                .Build(CancellationToken);

            var client = new DiscoveryClient(new StreamFactory());

            var sw = Stopwatch.StartNew();
            await AssertionExtensions.Should(() => client.DiscoverAsync(
                    new ServiceEndPoint(clientAndService.GetServiceEndPoint().BaseUri, "", clientAndService.Client!.TimeoutsAndLimits), 
                    clientAndService.Client!.TimeoutsAndLimits, 
                    CancellationToken))
                .ThrowAsync<HalibutClientException>();

            sw.Stop();
            sw.Elapsed.Should().BeCloseTo(clientAndService.Service!.TimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout, TimeSpan.FromSeconds(15), "Since a paused connection early on should not hang forever.");
        }
    }
}

using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class DiscoveryClientFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false)]
        public async Task DiscoverMethodReturnsEndpointDetails(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .Build(CancellationToken);

            var client = new DiscoveryClient();

#pragma warning disable CS0612
            var discovered = await clientAndServiceTestCase.SyncOrAsync
                .WhenSync(() => client.Discover(new ServiceEndPoint(clientAndService.GetServiceEndPoint().BaseUri, ""), CancellationToken))
                .WhenAsync(async () => await client.DiscoverAsync(new ServiceEndPoint(clientAndService.GetServiceEndPoint().BaseUri, ""), CancellationToken));
#pragma warning restore CS0612

            discovered.RemoteThumbprint.Should().BeEquivalentTo(clientAndService.GetServiceEndPoint().RemoteThumbprint);
            discovered.BaseUri.Should().BeEquivalentTo(clientAndService.GetServiceEndPoint().BaseUri);
        }

        [Test]
        [SyncAndAsync]
        public async Task DiscoveringNonExistantEndpointThrows(SyncOrAsync syncOrAsync)
        {
            var client = new DiscoveryClient();
            var fakeEndpoint = new ServiceEndPoint("https://fake-tentacle.example", "");

#pragma warning disable CS0612
            await syncOrAsync
                .WhenSync(() => Assert.Throws<HalibutClientException>(() => client.Discover(fakeEndpoint), "No such host is known")).IgnoreResult()
                .WhenAsync(async () => await AssertAsync.Throws<HalibutClientException>(() => client.DiscoverAsync(fakeEndpoint, CancellationToken), "No such host is known"));
#pragma warning restore CS0612
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling:false)]
        public async Task OctopusCanDiscoverTentacle(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .Build(CancellationToken))
            {
                var info = await clientAndServiceTestCase.SyncOrAsync
                    .WhenSync(() => clientAndService.Client.Discover(clientAndService.ServiceUri))
                    .WhenAsync(async () => await clientAndService.Client.DiscoverAsync(clientAndService.ServiceUri, CancellationToken));
                    
                info.RemoteThumbprint.Should().Be(Certificates.TentacleListeningPublicThumbprint);
            }
        }
    }
}

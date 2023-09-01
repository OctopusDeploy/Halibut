using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Observability;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Observability
{
    public class ConnectionObserverFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task ObserveConnections(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var connectionsObserver = new CountingConnectionsObserver();
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithConnectionObserverOnTcpServer(connectionsObserver)
                             .WithPortForwarding()
                             .WithInstantReconnectPollingRetryPolicy()
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();
                await echo.SayHelloAsync("hello");
                connectionsObserver.ListenerAcceptedConnectionCount.Should().Be(1);
                connectionsObserver.ClientReachedMessageExchangeCount.Should().Be(1);
                connectionsObserver.PreviouslyAcceptedConnectionFailedToInitializeCount.Should().Be(0);
                connectionsObserver.PreviouslyAcceptedConnectionHasBeenDisconnectedCount.Should().Be(0);
                
                clientAndService.PortForwarder!.CloseExistingConnections();
                
                await Try.CatchingError(() => echo.SayHelloAsync("hello"));
                await echo.SayHelloAsync("hello");
                
                connectionsObserver.ListenerAcceptedConnectionCount.Should().Be(2);
                connectionsObserver.ClientReachedMessageExchangeCount.Should().Be(2);
                connectionsObserver.PreviouslyAcceptedConnectionFailedToInitializeCount.Should().Be(0);
                connectionsObserver.PreviouslyAcceptedConnectionHasBeenDisconnectedCount.Should().Be(1);
            }
        }
        
        // TODO mess with the certs to get a count of failed to initialize connections
        
        // TODO add a pending request queue factory that throws as this counts as failed to reach message exchange
    }
}
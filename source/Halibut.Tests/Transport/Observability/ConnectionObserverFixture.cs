using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Observability
{
    public class ConnectionObserverFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task ObserveConnections(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var connectionsObserver = new TestConnectionsObserver();
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
                connectionsObserver.ConnectionAcceptedCount.Should().Be(1);
                connectionsObserver.ConnectionClosedCount.Should().Be(0);
                
                clientAndService.PortForwarder!.CloseExistingConnections();
                
                await Try.CatchingError(() => echo.SayHelloAsync("hello"));
                await echo.SayHelloAsync("hello");
                
                connectionsObserver.ConnectionAcceptedCount.Should().Be(2);
                connectionsObserver.ConnectionClosedCount.Should().Be(1);
            }
        }
    }
}
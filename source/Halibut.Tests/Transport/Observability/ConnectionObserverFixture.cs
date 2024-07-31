using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.TestServices.SyncClientWithOptions;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Observability
{
    public class ConnectionObserverFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task ObserveAuthorizedConnections(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var connectionsObserver = new TestConnectionsObserver();
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithConnectionObserverOnTcpServer(connectionsObserver)
                             .WithPortForwarding(out var portForwarderRef)
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await echo.SayHelloAsync("hello");
                connectionsObserver.ConnectionAcceptedCount.Should().Be(1);
                connectionsObserver.ConnectionClosedCount.Should().Be(0);
                
                portForwarderRef.Value.CloseExistingConnections();
                
                await Try.CatchingError(() => echo.SayHelloAsync("hello"));
                await echo.SayHelloAsync("hello");
                
                connectionsObserver.ConnectionAcceptedCount.Should().Be(2);
                connectionsObserver.ConnectionClosedCount.Should().Be(1);
            }

            Wait.UntilActionSucceeds(() =>
            {
                connectionsObserver.ConnectionAcceptedCount.Should().Be(2);
                connectionsObserver.ConnectionClosedCount.Should().Be(2);
            }, TimeSpan.FromSeconds(30), Logger, CancellationToken);

            connectionsObserver.ConnectionAcceptedAuthorized.Should().AllSatisfy(a => a.Should().BeTrue());
            connectionsObserver.ConnectionClosedAuthorized.Should().AllSatisfy(a => a.Should().BeTrue());
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false)]
        public async Task ObserveUnauthorizedListeningConnections(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var connectionsObserver = new TestConnectionsObserver();
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithServiceTrustingTheWrongCertificate()
                             .WithConnectionObserverOnTcpServer(connectionsObserver)
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await AssertionExtensions.Should(() => echo.SayHelloAsync("hello")).ThrowAsync<HalibutClientException>();

                connectionsObserver.ConnectionAcceptedCount.Should().BeGreaterOrEqualTo(1);
                connectionsObserver.ConnectionClosedCount.Should().BeGreaterOrEqualTo(1);

                connectionsObserver.ConnectionAcceptedAuthorized.Should().AllSatisfy(a => a.Should().BeFalse());
                connectionsObserver.ConnectionClosedAuthorized.Should().AllSatisfy(a => a.Should().BeFalse());
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task ObserveUnauthorizedPollingConnections(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var connectionsObserver = new TestConnectionsObserver();
            await using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithConnectionObserverOnTcpServer(connectionsObserver)
                             .Build(CancellationToken))
            {
                clientAndBuilder.Client.TrustOnly(new List<string>());

                using var cts = new CancellationTokenSource();
                var token = cts.Token;
                var echo = clientAndBuilder.CreateAsyncClient<IEchoService, IAsyncClientEchoServiceWithOptions>(
                    point => { point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(2000); });

                var sayHelloTask = Task.Run(async () => await echo.SayHelloAsync("hello", new HalibutProxyRequestOptions(token)), CancellationToken);

                await Task.Delay(3000, CancellationToken);

                await cts.CancelAsync();

                await AssertException.Throws<Exception>(sayHelloTask);

                connectionsObserver.ConnectionAcceptedCount.Should().BeGreaterOrEqualTo(1);
                connectionsObserver.ConnectionClosedCount.Should().BeGreaterOrEqualTo(1);

                connectionsObserver.ConnectionAcceptedAuthorized.Should().AllSatisfy(a => a.Should().BeFalse());
                connectionsObserver.ConnectionClosedAuthorized.Should().AllSatisfy(a => a.Should().BeFalse());
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testPolling: false, testListening: false)]
        public async Task ObserveUnauthorizedPollingWebSocketConnections(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var connectionsObserver = new TestConnectionsObserver();
            await using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithServiceTrustingTheWrongCertificate()
                             .WithConnectionObserverOnTcpServer(connectionsObserver)
                             .Build(CancellationToken))
            {
                using var cts = new CancellationTokenSource();
                var token = cts.Token;
                var echo = clientAndBuilder.CreateAsyncClient<IEchoService, IAsyncClientEchoServiceWithOptions>(
                    point => { point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(2000); });

                var sayHelloTask = Task.Run(async () => await echo.SayHelloAsync("hello", new HalibutProxyRequestOptions(token)), CancellationToken);

                await Task.Delay(3000, CancellationToken);

                await cts.CancelAsync();

                await AssertException.Throws<Exception>(sayHelloTask);

                connectionsObserver.ConnectionAcceptedCount.Should().BeGreaterOrEqualTo(1);
                connectionsObserver.ConnectionClosedCount.Should().BeGreaterOrEqualTo(1);

                connectionsObserver.ConnectionAcceptedAuthorized.Should().AllSatisfy(a => a.Should().BeFalse());
                connectionsObserver.ConnectionClosedAuthorized.Should().AllSatisfy(a => a.Should().BeFalse());
            }
        }
    }
}
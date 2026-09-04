using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
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
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task ConnectionsCountForAPollingSubscriptionChangesOneConnectionAtATime(ClientAndServiceTestCase clientAndServiceTestCase)
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

                var openFirstConnection = connectionsObserver.ConnectionsCountChangedForSubscription.First();
                openFirstConnection.PreviousCount.Should().Be(0);
                openFirstConnection.CurrentCount.Should().Be(1);
                openFirstConnection.SubscriptionId.Should().Be(clientAndService.ServiceUri);


                portForwarderRef.Value.CloseExistingConnections();

                await Try.CatchingError(() => echo.SayHelloAsync("hello"));

                var closeFirstConnection = connectionsObserver.ConnectionsCountChangedForSubscription.Skip(1).First();
                closeFirstConnection.PreviousCount.Should().Be(1);
                closeFirstConnection.CurrentCount.Should().Be(0);
                closeFirstConnection.SubscriptionId.Should().Be(clientAndService.ServiceUri);

                await echo.SayHelloAsync("hello");

                var openSecondConnection = connectionsObserver.ConnectionsCountChangedForSubscription.Skip(2).First();
                openSecondConnection.PreviousCount.Should().Be(0);
                openSecondConnection.CurrentCount.Should().Be(1);
                openSecondConnection.SubscriptionId.Should().Be(clientAndService.ServiceUri);
            }

            Wait.UntilActionSucceeds(() =>
            {
                var closeSecondConnection = connectionsObserver.ConnectionsCountChangedForSubscription.Skip(3).First();
                closeSecondConnection.PreviousCount.Should().Be(1);
                closeSecondConnection.CurrentCount.Should().Be(0);
            }, TimeSpan.FromSeconds(30), Logger, CancellationToken);
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false)]
        public async Task ConnectionsCountIsNotChangedForListeningConnections(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var connectionsObserver = new TestConnectionsObserver();
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .WithStandardServices()
                .AsLatestClientAndLatestServiceBuilder()
                .WithConnectionObserverOnTcpServer(connectionsObserver)
                .Build(CancellationToken);

            var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
            await echo.SayHelloAsync("hello");

            connectionsObserver.ConnectionsCountChangedForSubscription.Should().BeEmpty("only polling subscriptions lease a counted connection");
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
                             .WithClientTrustingNoThumbprints()
                             .WithConnectionObserverOnTcpServer(connectionsObserver)
                             .Build(CancellationToken))
            {

                using var cts = new CancellationTokenSource();
                var token = cts.Token;
                var echo = clientAndBuilder.CreateAsyncClient<IEchoService, IAsyncClientEchoServiceWithOptions>(
                    point => { point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(2000); });

                var sayHelloTask = Task.Run(async () => await echo.SayHelloAsync("hello", new HalibutProxyRequestOptions(token)), CancellationToken);

                await Task.Delay(3000, CancellationToken);

#if NET8_0_OR_GREATER
                await cts.CancelAsync();
#else
                cts.Cancel();
#endif
                
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

#if NET8_0_OR_GREATER
                await cts.CancelAsync();
#else
                cts.Cancel();
#endif
                await AssertException.Throws<Exception>(sayHelloTask);

                connectionsObserver.ConnectionAcceptedCount.Should().BeGreaterOrEqualTo(1);
                connectionsObserver.ConnectionClosedCount.Should().BeGreaterOrEqualTo(1);

                connectionsObserver.ConnectionAcceptedAuthorized.Should().AllSatisfy(a => a.Should().BeFalse());
                connectionsObserver.ConnectionClosedAuthorized.Should().AllSatisfy(a => a.Should().BeFalse());
            }
        }
    }
}
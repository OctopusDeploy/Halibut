using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class ListeningServiceErrorConnectionStateFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling:false)]
        public async Task WhenConnectingToAListeningService_AndAllAttemptsFailToConnect_TheHalibutClientExceptionShouldHaveAConnectionStatusOfConnecting(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithPortForwarding(out var portForwarderRef)
                       .WithEchoService()
                       .Build(CancellationToken))
            {
                portForwarderRef.Value.Dispose();

                var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.Zero;
                    point.ConnectionErrorRetryTimeout = TimeSpan.FromSeconds(5);
                    point.RetryCountLimit = 5;
                });
                
                (await AssertException.Throws<HalibutClientException>(() => echoService.SayHelloAsync("hello")))
                    .And.ConnectionState.Should().Be(ConnectionState.Connecting);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling:false)]
        public async Task WhenConnectingToAListeningService_AndAtLeastOneAttemptEstablishesAConnection_TheHalibutClientExceptionShouldHaveAConnectionStatusOfUnknown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithPortForwarding(out var portForwarderRef)
                             .WithDoSomeActionService(() =>
                             {
                                 portForwarderRef.Value.EnterKillNewAndExistingConnectionsMode();
                             })
                             .Build(CancellationToken))
            {
                var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.Zero;
                    point.ConnectionErrorRetryTimeout = TimeSpan.FromSeconds(5);
                    point.RetryCountLimit = 5;
                });
                
                (await AssertException.Throws<HalibutClientException>(() => echoService.SayHelloAsync("hello")))
                    .And.ConnectionState.Should().Be(ConnectionState.Unknown);
            }
        }
    }
}

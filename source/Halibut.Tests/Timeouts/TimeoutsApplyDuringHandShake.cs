using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Builders;
using Halibut.Tests.Support;
using Halibut.Tests.Support.PortForwarding;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Timeouts
{
    /// <summary>
    /// Where handshake means early on in setting up a TCP connection.
    /// </summary>
    public class TimeoutsApplyDuringHandShake : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, additionalParameters: new object[] { true, 1 })]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, additionalParameters: new object[] { false, 1 })]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, additionalParameters: new object[] { true, 2 })]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, additionalParameters: new object[] { false, 2 })]
        public async Task WhenTheFirstWriteOverTheWireOccursOnAConnectionThatImmediatelyPauses_AWriteTimeoutShouldApply(
            ClientAndServiceTestCase clientAndServiceTestCase,
            bool onClientToOrigin, // Don't dwell on what this means, we just want to test all combinations of where the timeout can occur.
            int writeNumberToPauseOn // Ie pause on the first or second write
            ) 
        {
            var dataTransferObserverPauser = new DataTransferObserverBuilder()
                .WithWritePausing(Logger, writeNumberToPauseOn)
                .Build();
            var dataTransferObserverDoNothing = new DataTransferObserverBuilder().Build();

            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build().WithAllTcpTimeoutsTo(TimeSpan.FromMinutes(20));
            halibutTimeoutsAndLimits.TcpClientTimeout = new SendReceiveTimeout(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            
            TcpConnectionsCreatedCounter? tcpConnectionsCreatedCounter = null;
            
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port)
                           .WithDataObserver(() =>
                           {
                               if(onClientToOrigin) return new BiDirectionalDataTransferObserver(dataTransferObserverPauser,dataTransferObserverDoNothing);
                               return new BiDirectionalDataTransferObserver(dataTransferObserverDoNothing, dataTransferObserverPauser);
                           })
                           .WithCountTcpConnectionsCreated(out tcpConnectionsCreatedCounter)
                           .Build())
                       .WithPendingRequestQueueFactory(logFactory => new FuncPendingRequestQueueFactory(uri => new PendingRequestQueueBuilder()
                           .WithLog(logFactory.ForEndpoint(uri))
                           .WithPollingQueueWaitTimeout(TimeSpan.FromSeconds(1))
                           .Build()))
                       .WithEchoService()
                       .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(IncreasePollingQueueTimeout);
                var sw = Stopwatch.StartNew();
                if (clientAndServiceTestCase.ServiceConnectionType == ServiceConnectionType.Listening)
                {
                    // Under listening we will always see the connection fail through the request.
                    try
                    {
                        await echo.SayHelloAsync("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");
                    }
                    catch (Exception e)
                    {
                        Logger.Information(e, "An exception was raised during the request, this is not an issue since we are concerned about the timings. " +
                                              "Exceptions only occur occasionally and probably only on listening.");
                    }
                }
                else
                {
                    // For polling we can end up in a state in which we do pull something of the queue because the client is
                    // happy with the service and moves on to getting stuff out of the queue, however the service is still waiting
                    // to authentice the client but can't since the TCP connection is paused. 
                    // So instead lets count created TCP connections.
                    Wait.UntilActionSucceeds(() => tcpConnectionsCreatedCounter!.ConnectionsCreatedCount.Should().BeGreaterThanOrEqualTo(2), TimeSpan.FromSeconds(30), Logger, CancellationToken);
                }

                sw.Stop();
                sw.Elapsed.Should()
                    .BeCloseTo(halibutTimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout, TimeSpan.FromSeconds(15), "Since a paused connection early on should not hang forever.");

                await echo.SayHelloAsync("The pump wont be paused here so this should work.");
            }
        }
        
        static void IncreasePollingQueueTimeout(ServiceEndPoint point)
        {
            // We don't want to measure the polling queue timeouts.
            point.PollingRequestQueueTimeout = TimeSpan.FromMinutes(10);
            point.RetryCountLimit = 1;
        }
    }
}

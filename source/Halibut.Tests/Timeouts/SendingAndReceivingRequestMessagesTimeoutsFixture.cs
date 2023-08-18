using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.PortForwarding;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
<<<<<<< HEAD
using NUnit.Framework;
=======
using LogLevel = Halibut.Logging.LogLevel;
>>>>>>> 48525a3 (WIP)

namespace Halibut.Tests.Timeouts
{
    public class SendingAndReceivingRequestMessagesTimeoutsFixture : BaseTest
    {
        [Test]
<<<<<<< HEAD
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false)]
=======
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false,  testAsyncServicesAsWell: true, testSyncService : false, testSyncClients: false, testAsyncClients: true)]
>>>>>>> 48525a3 (WIP)
        public async Task HalibutTimeoutsAndLimits_AppliesToTcpClientReceiveTimeout(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var expectedTimeout = TimeSpan.FromSeconds(10);
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port).WithPortForwarderDataLogging(clientAndServiceTestCase.ServiceConnectionType).Build())
                       .WithPortForwarding(out var portForwarderRef)
                       .WithEchoService()
                       .WithDoSomeActionService(() => portForwarderRef.Value.PauseExistingConnections())
                       .WhenTestingAsyncClient(clientAndServiceTestCase, b =>
                       {
                           b.WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build()
                               .SetAllTcpTimeoutsTo(TimeSpan.FromSeconds(133))
                               .WithTcpClientReceiveTimeout(expectedTimeout));
                       })
                       .WithInstantReconnectPollingRetryPolicy()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await echo.SayHelloAsync("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                var pauseConnections = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionService>(IncreasePollingQueueTimeout());

                var sw = Stopwatch.StartNew();
                var e = (await AssertAsync.Throws<HalibutClientException>(async () => await pauseConnections.ActionAsync())).And;
                sw.Stop();
                Logger.Error(e, "Received error");
                AssertExceptionMessageLooksLikeAReadTimeout(e);
                sw.Elapsed.Should().BeGreaterThan(expectedTimeout - TimeSpan.FromSeconds(2), "The receive timeout should apply, not the shorter heart beat timeout") // -2s give it a little slack to avoid it timed out slightly too early.
                    .And
                    .BeLessThan(expectedTimeout + HalibutTimeoutsAndLimitsForTestsBuilder.HalfTheTcpReceiveTimeout, "We should be timing out on the tcp receive timeout");
                
                // The polling tentacle, will not reconnect in time since it has a 133s receive control message timeout.
                // To move it along we, kill the connection here.
                // Interestingly this tests does not tests the service times out (the below test does).
                clientAndService.PortForwarder.CloseExistingConnections();

                await echo.SayHelloAsync("A new request can be made on a new unpaused TCP connection");
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenThenNetworkIsPaused_WhileReadingAResponseMessage_ATcpReadTimeoutOccurs_and_FurtherRequestsCanBeMade(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(out var portForwarderRef)
                       .WithEchoService()
                       .WithDoSomeActionService(() => portForwarderRef.Value.PauseExistingConnections())
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await echo.SayHelloAsync("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                var pauseConnections = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionService>(IncreasePollingQueueTimeout());

                var sw = Stopwatch.StartNew();
                var e = (await AssertAsync.Throws<HalibutClientException>(async () => await pauseConnections.ActionAsync())).And;
                sw.Stop();
                Logger.Error(e, "Received error");
                AssertExceptionMessageLooksLikeAReadTimeout(e);
                sw.Elapsed.Should().BeGreaterThan(clientAndService.Service.TimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout - TimeSpan.FromSeconds(2), "The receive timeout should apply, not the shorter heart beat timeout") // -2s give it a little slack to avoid it timed out slightly too early.
                    .And
                    .BeLessThan(clientAndService.Service.TimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout + HalibutTimeoutsAndLimitsForTestsBuilder.HalfTheTcpReceiveTimeout, "We should be timing out on the tcp receive timeout");
                
                await echo.SayHelloAsync("A new request can be made on a new unpaused TCP connection");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenThenNetworkIsPaused_WhileReadingAResponseMessageDataStream_ATcpReadTimeoutOccurs_and_FurtherRequestsCanBeMade(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(out var portForwarderRef)
                       .WithEchoService()
                       .WithReturnSomeDataStreamService(() => DataStreamUtil.From(
                           firstSend: "hello",
                           andThenRun: portForwarderRef.Value!.PauseExistingConnections,
                           thenSend: "All done"))
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await echo.SayHelloAsync("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                var pauseConnections = clientAndService.CreateAsyncClient<IReturnSomeDataStreamService, IAsyncClientReturnSomeDataStreamService>(IncreasePollingQueueTimeout());

                var sw = Stopwatch.StartNew();
                var e = (await AssertAsync.Throws<HalibutClientException>(async () => await pauseConnections.SomeDataStreamAsync())).And;
                sw.Stop();
                Logger.Error(e, "Received error");
                AssertExceptionMessageLooksLikeAReadTimeout(e);
                sw.Elapsed.Should().BeGreaterThan(clientAndService.Service.TimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout - TimeSpan.FromSeconds(2), "The receive timeout should apply, not the shorter heart beat timeout") // -2s give it a little slack to avoid it timed out slightly too early.
                    .And
                    .BeLessThan(clientAndService.Service.TimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout + HalibutTimeoutsAndLimitsForTestsBuilder.HalfTheTcpReceiveTimeout, "We should be timing out on the tcp receive timeout");

                await echo.SayHelloAsync("A new request can be made on a new unpaused TCP connection");
            }
        }

        [Test]
        [Timeout(120000)]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenThenNetworkIsPaused_WhileSendingARequestMessage_ATcpWriteTimeoutOccurs_and_FurtherRequestsCanBeMade(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var numberOfBytesBeforePausingAStream = 1024 * 1024; // 1MB

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port)
                           .PauseSingleStreamAfterANumberOfBytesHaveBeenSet(numberOfBytesBeforePausingAStream)
                           .Build())
                       .WithEchoService()
                       .WithHalibutLoggingLevel(LogLevel.Trace)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await echo.SayHelloAsync("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                var echoServiceTheErrorWillHappenOn = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(IncreasePollingQueueTimeout());
                
                var sw = Stopwatch.StartNew();
                var e = (await AssertAsync.Throws<HalibutClientException>(() =>
                {
                    var stringToSend = Some.RandomAsciiStringOfLength(numberOfBytesBeforePausingAStream * 20);
                    return echoServiceTheErrorWillHappenOn.SayHelloAsync(stringToSend);
                })).And;
                AssertExceptionLooksLikeAWriteTimeout(e);
                sw.Stop();
                Logger.Error(e, "Received error when making the request (as expected)");
                
                var expectedTimeOut = clientAndService.Service.TimeoutsAndLimits.TcpClientTimeout.SendTimeout;

                sw.Elapsed.Should().BeGreaterThan(
                        expectedTimeOut - TimeSpan.FromSeconds(2), 
                        "Should wait the send timeout amount of time NOT the heart beat timeout") // -2s give it a little slack to avoid it timed out slightly too early.
                    .And
                    .BeLessThan(
                        expectedTimeOut + HalibutTimeoutsAndLimitsForTestsBuilder.HalfTheTcpReceiveTimeout,
                        "Should wait the send timeout amount of time");
                
                await echo.SayHelloAsync("A new request can be made on a new unpaused TCP connection");
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenThenNetworkIsPaused_WhileSendingADataStreamAsPartOfARequestMessage_ATcpWriteTimeoutOccurs_and_FurtherRequestsCanBeMade(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(out var portForwarderRef)
                       .WithEchoService()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await echo.SayHelloAsync("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                var echoServiceTheErrorWillHappenOn = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(IncreasePollingQueueTimeout());
                
                var sw = Stopwatch.StartNew();
                var e = (await AssertAsync.Throws<HalibutClientException>(async () => await echoServiceTheErrorWillHappenOn.CountBytesAsync(DataStreamUtil.From(
                    firstSend: "hello",
                    andThenRun: portForwarderRef.Value!.PauseExistingConnections,
                    thenSend: "All done" + Some.RandomAsciiStringOfLength(10*1024*1024)
                )))).And;

                AssertExceptionLooksLikeAWriteTimeout(e);
                
                sw.Stop();
                Logger.Error(e, "Received error when making the request (as expected)");
                
                var expectedTimeout = clientAndService.Service.TimeoutsAndLimits.TcpClientTimeout.SendTimeout;
                sw.Elapsed.Should().BeGreaterThan(expectedTimeout - TimeSpan.FromSeconds(2), "The receive timeout should apply, not the shorter heart beat timeout") // -2s give it a little slack to avoid it timed out slightly too early.
                    .And
                    .BeLessThan(expectedTimeout + HalibutTimeoutsAndLimitsForTestsBuilder.HalfTheTcpReceiveTimeout, "We should be timing out on the tcp receive timeout");

                await echo.SayHelloAsync("A new request can be made on a new unpaused TCP connection");
            }
        }

        static void AssertExceptionLooksLikeAWriteTimeout(HalibutClientException? e)
        {
            e.Message.Should().ContainAny(
                "Unable to write data to the transport connection: Connection timed out.",
                " Unable to write data to the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond");

            e.IsNetworkError().Should().Be(HalibutNetworkExceptionType.IsNetworkError);
        }

        static void AssertExceptionMessageLooksLikeAReadTimeout(HalibutClientException? e)
        {
            e.Message.Should().ContainAny(
                "Unable to read data from the transport connection: Connection timed out.",
                "Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.");
            
            e.IsNetworkError().Should().Be(HalibutNetworkExceptionType.IsNetworkError);
        }

        static Action<ServiceEndPoint> IncreasePollingQueueTimeout()
        {
            return point =>
            {
                // We don't want to measure the polling queue timeouts.
                point.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromMinutes(10);
                point.PollingRequestQueueTimeout = TimeSpan.FromMinutes(10);
            };
        }
    }
}

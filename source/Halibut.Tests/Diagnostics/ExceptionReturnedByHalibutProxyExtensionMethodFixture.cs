using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests.Diagnostics
{
    public static class ExceptionReturnedByHalibutProxyExtensionMethodFixture
    {
        public class WhenGivenA
        {
            [Test]
            public void MethodNotFoundHalibutClientException_ItIsNotARetryableError()
            {
                new MethodNotFoundHalibutClientException("").IsRetryableError()
                    .Should()
                    .Be(HalibutRetryableErrorType.NotRetryable);
            }
            
            [Test]
            public void SocketExceptionError_ItIsARetryableError()
            {
                new SocketException(123).IsRetryableError()
                    .Should()
                    .Be(HalibutRetryableErrorType.IsRetryable);
            }
            
            [Test]
            public void SomeRandomException_ItIsARetryableError()
            {
                new Exception("Totally random").IsRetryableError()
                    .Should()
                    .Be(HalibutRetryableErrorType.UnknownError);
            }
            
            [Test]
            public void MethodNotFoundHalibutClientException_ItIsNotANetworkError()
            {
                new MethodNotFoundHalibutClientException("").IsNetworkError()
                    .Should()
                    .Be(HalibutNetworkExceptionType.NotANetworkError);
            }

            [Test]
            public void ServiceNotFoundHalibutClientException_ItIsNotANetworkError()
            {
                new ServiceNotFoundHalibutClientException("").IsNetworkError()
                    .Should()
                    .Be(HalibutNetworkExceptionType.NotANetworkError);
            }

            [Test]
            public void AmbiguousMethodMatchHalibutClientException_ItIsNotANetworkError()
            {
                new AmbiguousMethodMatchHalibutClientException("").IsNetworkError()
                    .Should()
                    .Be(HalibutNetworkExceptionType.NotANetworkError);
            }
        }

        public class WhenTheHalibutProxyThrowsAnException : BaseTest
        {
            [LatestClientAndLatestServiceTestCases(testNetworkConditions:false)]
            public async Task WhenTheConnectionTerminatesWaitingForAResponse(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .WithPortForwarding(out var portForwarder)
                           .WithDoSomeActionService(() => portForwarder.Value.EnterKillNewAndExistingConnectionsMode())
                           .Build(CancellationToken))
                {
                    var svc = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionService>();

                    // When svc.Action() is executed, tentacle will kill the TCP connection and dispose the port forwarder preventing new connections.
                    var exception = (await AssertException.Throws<HalibutClientException>(async () => await svc.ActionAsync())).And;
                    new SerilogLoggerBuilder().Build().Information(exception, "Got an exception, we were expecting one");
                    exception.IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.IsNetworkError);

                    exception.IsRetryableError()
                        .Should()
                        .Be(HalibutRetryableErrorType.IsRetryable);
                    
                        exception.Message.Should().ContainAny(new []
                            {
                                "Attempted to read past the end of the stream.",
                                "Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.",
                                "The I/O operation has been aborted because of either a thread exit or an application request",
                                "The remote party closed the WebSocket connection without completing the close handshake"
                            },
                            because: "This isn't the best message, really the connection was closed before we got the data we were expecting resulting in us reading past the end of the stream");
                }
            }
            
            [LatestClientAndLatestServiceTestCases(testNetworkConditions:false)]
            public async Task WhenTheConnectionPausesWaitingForAResponse(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .WithPortForwarding(out var portForwarder)
                           .WithDoSomeActionService(() => portForwarder.Value.PauseExistingConnections())
                           .Build(CancellationToken))
                {
                    var svc = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionService>();

                    // When svc.Action() is executed, tentacle will kill the TCP connection and dispose the port forwarder preventing new connections.
                    var exception = (await AssertException.Throws<HalibutClientException>(() => svc.ActionAsync())).And;
                    new SerilogLoggerBuilder().Build().Information(exception, "Got an exception, we were expecting one");
                    exception.IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.IsNetworkError);
                    
                    exception.Message.Should().ContainAny(
                        "Unable to read data from the transport connection: Connection timed out.", 
                        "Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.");
                }
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening:false)]
            public async Task BecauseThePollingRequestWasNotCollected(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using (var client = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder().Build(CancellationToken))
                {
                    var echo = client.CreateClientWithoutService<IEchoService, IAsyncClientEchoService>(point => point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(1));

                    (await AssertException.Throws<HalibutClientException>(async () => await echo.SayHelloAsync("Hello"))).And
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.UnknownError);
                }
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testPolling: false, testWebSocket: false)]
            public async Task BecauseTheListeningTentacleIsNotResponding(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using (var client = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder().Build(CancellationToken))
                {
                    var echo = client.CreateClientWithoutService<IEchoService, IAsyncClientEchoService>(serviceEndPoint => { serviceEndPoint.RetryCountLimit = 1; });

                    (await AssertException.Throws<HalibutClientException>(async () => await echo.SayHelloAsync("Hello"))).And
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.IsNetworkError);
                }
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false)]
            public async Task BecauseTheProxyIsNotResponding_TheExceptionShouldBeANetworkError(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .WithStandardServices()
                           .WithProxy(out var proxyService)
                           .Build(CancellationToken))
                {
                    proxyService.Value.Dispose();

                    var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(point =>
                    {
                        point.RetryCountLimit = 1;
                    });
                    (await AssertException.Throws<HalibutClientException>(async () => await echo.SayHelloAsync("Hello")))
                        .And
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.IsNetworkError);
                }
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testPolling: false, testWebSocket: false)]
            public async Task BecauseOfAInvalidCertificateException_WhenConnectingToListening_ItIsNotANetworkError(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .WithClientTrustingTheWrongCertificate()
                           .WithEchoService()
                           .Build(CancellationToken))
                {
                    var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                    (await AssertException.Throws<HalibutClientException>(async () => await echo.SayHelloAsync("Hello")))
                        .And
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
            public async Task BecauseTheDataStreamHadAnErrorOpeningTheFileWithFileStream_WhenSending_ItIsNotANetworkError(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .WithStandardServices()
                           .Build(CancellationToken))
                {
                    var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                    var dataStream = new DataStream(10, 
                        async (_, _) =>
                            {
                                await Task.CompletedTask;
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                                new FileStream("DoesNotExist2497546", FileMode.Open).Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method

                            });

                    (await AssertException.Throws<HalibutClientException>(async () => await echo.CountBytesAsync(dataStream)))
                        .And
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
            public async Task BecauseTheDataStreamThrowAFileNotFoundException_WhenSending_ItIsNotANetworkError(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .WithStandardServices()
                           .Build(CancellationToken))
                {
                    var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                    var dataStream = new DataStream(10, 
                        async (_, _) =>
                        {
                            await Task.CompletedTask.ConfigureAwait(false);
                            throw new FileNotFoundException();
                        });

                    (await AssertException.Throws<HalibutClientException>(() => echo.CountBytesAsync(dataStream)))
                        .And
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }

            [Test]
            [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false)]
            public async Task BecauseTheServiceThrowAnException_ItIsNotANetworkError(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .WithStandardServices()
                           .Build(CancellationToken))
                {
                    var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                    var exception = (await AssertException.Throws<ServiceInvocationHalibutClientException>(() => echo.CrashAsync())).And;
                    exception.IsNetworkError().Should().Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.PendingRequestQueueFactories;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    /// <summary>
    /// The type of exception that is throw by Halibut is important for the caller to be able to determine what went wrong and how to handle it.
    /// These tests ensure the contract is maintained in Halibut and that the exception does not change and have un-intended consequences for the caller.
    /// </summary>
    public class ExceptionContractFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task WhenThePollingRequestQueueTimeoutIsReached_AHalibutClientExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);

            await using var clientOnly = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder()
                .AsLatestClientBuilder()
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .Build(CancellationToken);

            var client = clientOnly.CreateClientWithoutService<IEchoService, IAsyncClientEchoServiceWithOptions>();

            (await AssertException.Throws<HalibutClientException>(async () => await client.SayHelloAsync("Hello", new(CancellationToken))))
                .And.Message.Should().Contain("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time (00:00:05), so the request timed out.");
        }
        

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task WhenThePollingRequestHasBegunTransfer_AndATcpTimeoutIsReached_AHalibutClientExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
            halibutTimeoutsAndLimits.TcpClientReceiveResponseTimeout = TimeSpan.FromSeconds(6);

            var waitSemaphore = new SemaphoreSlim(0, 1);

            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithDoSomeActionService(() => waitSemaphore.Wait(CancellationToken))
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .Build(CancellationToken);

            var doSomeActionClient = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

            (await AssertException.Throws<HalibutClientException>(async () => await doSomeActionClient.ActionAsync(new(CancellationToken))))
                .And.Message.Should().ContainAny(
                    "Unable to read data from the transport connection: Connection timed out.",
                    "Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond");

            waitSemaphore.Release();
        }

        [Test] 
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task WhenThePollingRequestIsCancelledWhileQueued_AnOperationCanceledExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            
            await using var clientOnly = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder()
                .AsLatestClientBuilder()
                .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestQueuedPendingRequestQueueFactory(inner, cancellationTokenSource)))
                .Build(CancellationToken);

            var client = clientOnly.CreateClientWithoutService<IEchoService, IAsyncClientEchoServiceWithOptions>();

            await AssertException.Throws<OperationCanceledException>(async () => await client.SayHelloAsync("Hello", new(cancellationTokenSource.Token)));
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task WhenThePollingRequestIsCancelledWhileDequeued_AnOperationCanceledExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();

            var cancellationTokenSource = new CancellationTokenSource();

            var waitSemaphore = new SemaphoreSlim(0, 1);
            
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .WithDoSomeActionService(() => waitSemaphore.Wait(CancellationToken))
                .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestDequeuedPendingRequestQueueFactory(inner, cancellationTokenSource)))
                .Build(CancellationToken);

            var doSomeActionClient = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

            await AssertException.Throws<OperationCanceledException>(async () => await doSomeActionClient.ActionAsync(new(cancellationTokenSource.Token)));

            waitSemaphore.Release();
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testPolling: false, testWebSocket: false)]
        public async Task WhenTheListeningRequestFailsToBeSent_AsTheServiceDoesNotAcceptTheConnection_AHalibutClientExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();

            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .WithPortForwarding(out var portForwarder)
                .Build(CancellationToken);

            portForwarder.Value.Dispose();

            var client = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoServiceWithOptions>();

            (await AssertException.Throws<HalibutClientException>(async () => await client.SayHelloAsync("Hello", new(CancellationToken))))
                .And.Message.Should().ContainAny(
                    $"An error occurred when sending a request to '{clientAndService.ServiceUri}', before the request could begin: No connection could be made because the target machine actively refused it",
                    $"An error occurred when sending a request to '{clientAndService.ServiceUri}', before the request could begin: Connection refused");
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testPolling: false, testWebSocket: false)]
        public async Task WhenTheListeningRequestFailsToBeSent_AsTheServiceRejectsTheConnection_AHalibutClientExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();

            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithStandardServices()
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .WithPortForwarding(out var portForwarder)
                .Build(CancellationToken);

            portForwarder.Value.EnterKillNewAndExistingConnectionsMode();

            var client = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoServiceWithOptions>();

            (await AssertException.Throws<HalibutClientException>(async () => await client.SayHelloAsync("Hello", new(CancellationToken))))
                .And.Message.Should().ContainAny(
                    $"An error occurred when sending a request to '{clientAndService.ServiceUri}', before the request could begin: Unable to read data from the transport connection",
                    $"An error occurred when sending a request to '{clientAndService.ServiceUri}', before the request could begin: Unable to write data to the transport connection",
                    $"An error occurred when sending a request to '{clientAndService.ServiceUri}', before the request could begin:  Received an unexpected EOF or 0 bytes from the transport stream",
                    $"An error occurred when sending a request to '{clientAndService.ServiceUri}', before the request could begin: Transport endpoint is not connected");
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testPolling: false, testWebSocket: false)]
        public async Task WhenTheListeningRequestFailsToBeSent_AsTheConnectionAttemptToTheServiceTimesOut_AHalibutClientExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();

            halibutTimeoutsAndLimits.TcpClientConnectTimeout = TimeSpan.FromSeconds(1);

            await using var clientOnly = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder()
                .AsLatestClientBuilder()
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .Build(CancellationToken);

            // We need to use a non localhost address to get a timeout
            var client = clientOnly.CreateClient<IEchoService, IAsyncClientEchoServiceWithOptions>(new Uri("https://20.5.79.31:10933/"));

            (await AssertException.Throws<HalibutClientException>(async () => await client.SayHelloAsync("Hello", new(CancellationToken))))
                .And.Message.Should().Contain(
                    $"An error occurred when sending a request to 'https://20.5.79.31:10933/', before the request could begin: The client was unable to establish the initial connection within the timeout 00:00:01.");
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testPolling: false, testWebSocket: false)]
        public async Task WhenTheListeningRequestIsCancelledImmediatelyWhileTryingToConnect_AHalibutClientExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.TcpClientConnectTimeout = TimeSpan.FromSeconds(5);
            halibutTimeoutsAndLimits.ConnectionErrorRetryTimeout = TimeSpan.FromSeconds(600);
            halibutTimeoutsAndLimits.RetryCountLimit = int.MaxValue;
            
            await using var clientAndService = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder()
                .AsLatestClientBuilder()
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .Build(CancellationToken);

            var client = clientAndService.CreateClient<IEchoService, IAsyncClientEchoServiceWithOptions>(new Uri("https://20.5.79.31:10933/"));

            var cancellationTokenSource = new CancellationTokenSource();
            
            (await AssertException.Throws<ConnectingRequestCancelledException>(async () =>
            {
                var task = client.SayHelloAsync("Hello", new(cancellationTokenSource.Token));
#pragma warning disable VSTHRD103
                cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
                await task;
            })).And.Message.Should().Contain($"An error occurred when sending a request to 'https://20.5.79.31:10933/', after the request began: The Request was cancelled while Connecting.");
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testPolling: false, testWebSocket: false)]
        public async Task WhenTheListeningRequestIsCancelledWhileConnecting_AHalibutClientExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.TcpClientConnectTimeout = TimeSpan.FromSeconds(60);
            halibutTimeoutsAndLimits.ConnectionErrorRetryTimeout = TimeSpan.FromSeconds(600);
            halibutTimeoutsAndLimits.RetryCountLimit = int.MaxValue;

            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithStandardServices()
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .Build(CancellationToken);
            
            var client = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoServiceWithOptions>(point =>
            {
                // We need to use a non localhost address to get the correct exception
                return new ServiceEndPoint(new Uri("https://20.5.79.31:10933/"), point.RemoteThumbprint, point.Proxy, clientAndService.Client!.TimeoutsAndLimits);
            });

            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            (await AssertException.Throws<ConnectingRequestCancelledException>(async () => await client.SayHelloAsync("Hello", new(cancellationTokenSource.Token))))
                .And.Message.Should().Contain("An error occurred when sending a request to 'https://20.5.79.31:10933/', after the request began: The Request was cancelled while Connecting.");
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testPolling: false, testWebSocket: false)]
        public async Task WhenTheListeningRequestIsCancelledWhileConnecting_AndTheConnectionIsEstablishedButPaused_AHalibutClientExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.TcpClientConnectTimeout = TimeSpan.FromSeconds(60);
            halibutTimeoutsAndLimits.ConnectionErrorRetryTimeout = TimeSpan.FromSeconds(600);
            halibutTimeoutsAndLimits.RetryCountLimit = int.MaxValue;

            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithStandardServices()
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .WithPortForwarding(out var portForwarder)
                .Build(CancellationToken);
            
            portForwarder.Value.EnterPauseNewAndExistingConnectionsMode();
            var client = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoServiceWithOptions>();

            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var exception = (await AssertException.Throws<ConnectingRequestCancelledException>(async () => await client.SayHelloAsync("Hello", new(cancellationTokenSource.Token)))).And;
            exception.Message.Should().Be($"An error occurred when sending a request to '{clientAndService.ServiceUri}', after the request began: The Request was cancelled while Connecting.");
        }
        
// net48 does not support cancellation of the request as the DeflateStream ends up using Begin and End methods which don't get passed the cancellation token
#if !NETFRAMEWORK
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testPolling: false, testWebSocket: false)]
        public async Task WhenTheListeningRequestIsCancelledWhileInProgress_AnOperationCanceledExceptionShouldBeThrown(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var executingSemaphore = new SemaphoreSlim(0, 1);
            var waitSemaphore = new SemaphoreSlim(0, 1);
            
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithDoSomeActionService(() =>
                {
                    executingSemaphore.Release();
                    waitSemaphore.Wait(CancellationToken);
                })
                .Build(CancellationToken);

            var doSomeActionClient = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

            var cancellationTokenSource = new CancellationTokenSource();
            
            var cancellationTask = Task.Run(async () =>
            {
                await executingSemaphore.WaitAsync(CancellationToken);
#pragma warning disable VSTHRD103
                cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
            });

            (await AssertException.Throws<TransferringRequestCancelledException>(async () => await doSomeActionClient.ActionAsync(new(cancellationTokenSource.Token))))
                .And.Message.Should().Contain($"An error occurred when sending a request to '{clientAndService.ServiceUri}', after the request began: The Request was cancelled while Transferring.");

            waitSemaphore.Release();
            await cancellationTask;
        }
#endif
    }
}
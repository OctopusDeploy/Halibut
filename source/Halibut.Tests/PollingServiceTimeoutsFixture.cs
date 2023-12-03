using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class PollingServiceTimeoutsFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task WhenThePollingRequestQueueTimeoutIsReached_TheRequestShouldTimeout(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);

            await using (var clientOnly = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder()
                             .AsLatestClientBuilder()
                             .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                             .Build(CancellationToken))
            {
                var client = clientOnly.CreateClientWithoutService<IEchoService, IAsyncClientEchoServiceWithOptions>();

                var stopwatch = Stopwatch.StartNew();
                (await AssertException.Throws<HalibutClientException>(async () => await client.SayHelloAsync("Hello", new(CancellationToken, CancellationToken.None))))
                    .And.Message.Should().Contain("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time (00:00:05), so the request timed out.");
                stopwatch.Stop();

                stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(8), "Should have timed out quickly");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task WhenThePollingRequestQueueTimeoutIsReached_ButTheResponseIsReceivedBeforeThePollingRequestMaximumMessageProcessingTimeoutIsReached_TheRequestShouldSucceed(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
            halibutTimeoutsAndLimits.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(100);

            var responseDelay = TimeSpan.FromSeconds(10);

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .As<LatestClientAndLatestServiceBuilder>()
                             .WithDoSomeActionService(() => Thread.Sleep(responseDelay))
                             .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                             .WithInstantReconnectPollingRetryPolicy()
                             .Build(CancellationToken))
            {
                var doSomeActionClient = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

                var stopwatch = Stopwatch.StartNew();
                await doSomeActionClient.ActionAsync(new(CancellationToken, CancellationToken.None));
                stopwatch.Stop();

                stopwatch.Elapsed.Should()
                    .BeGreaterThan(halibutTimeoutsAndLimits.PollingRequestQueueTimeout, "Should have waited longer than the PollingRequestQueueTimeout");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task WhenThePollingRequestMaximumMessageProcessingTimeoutIsReached_TheRequestShouldTimeout_AndTheTransferringPendingRequestCancelled(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
            halibutTimeoutsAndLimits.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(6);

            var waitSemaphore = new SemaphoreSlim(0, 1);
            var connectionsObserver = new TestConnectionsObserver();

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .As<LatestClientAndLatestServiceBuilder>()
                             .WithDoSomeActionService(() => waitSemaphore.Wait(CancellationToken))
                             .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                             .WithInstantReconnectPollingRetryPolicy()
                             .WithConnectionObserverOnTcpServer(connectionsObserver)
                             .Build(CancellationToken))
            {
                var doSomeActionClient = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

                var stopwatch = Stopwatch.StartNew();
                (await AssertException.Throws<HalibutClientException>(async () => await doSomeActionClient.ActionAsync(new(CancellationToken, CancellationToken.None))))
                    .And.Message.Should().Contain("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time (00:00:06), so the request timed out.");
                stopwatch.Stop();

                stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15), "Should have timed out quickly");

                connectionsObserver.ConnectionAcceptedCount.Should().Be(1, "A single TCP connection should have been created");

                waitSemaphore.Release();

                Wait.UntilActionSucceeds(() =>
                {
                    // Leaving these asserts as when cooperative cancellation is supported it should cause them to fail at which point they can be fixed to assert cancellation to the socket works as expected.
                    connectionsObserver.ConnectionClosedCount.Should().Be(0, "Cancelling the PendingRequest does not cause the TCP Connection to be cancelled to stop the in-flight request");
                    connectionsObserver.ConnectionAcceptedCount.Should().Be(1, "The Service won't have reconnected after the request was cancelled");
                }, TimeSpan.FromSeconds(30), Logger, CancellationToken);
            }
        }
    }
}
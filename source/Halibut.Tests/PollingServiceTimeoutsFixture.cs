﻿using System;
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
using Halibut.Transport;
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
                var exception = (await AssertException.Throws<HalibutClientException>(async () => await client.SayHelloAsync("Hello", new(CancellationToken)))).And;

                exception.Message.Should().Contain("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time (00:00:05), so the request timed out. Please check the tentacle logs to investigate this timeout.");
                exception.ConnectionState.Should().Be(ConnectionState.Connecting);

                stopwatch.Stop();

                stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(8), "Should have timed out quickly");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task WhenThePollingRequestQueueTimeoutIsReached_ButTheResponseTriggersNoTcpTimeouts_TheRequestShouldSucceed(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);

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
                await doSomeActionClient.ActionAsync(new(CancellationToken));
                stopwatch.Stop();

                stopwatch.Elapsed.Should()
                    .BeGreaterThan(halibutTimeoutsAndLimits.PollingRequestQueueTimeout, "Should have waited longer than the PollingRequestQueueTimeout");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task WhenThePollingRequestQueueTimeoutIsReached_ButTheResponseIsReceivedAfterPollingRequestQueueTimeout_TheRequestShouldSucceed(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);

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
                await doSomeActionClient.ActionAsync(new(CancellationToken));
                stopwatch.Stop();

                stopwatch.Elapsed.Should()
                    .BeGreaterThan(halibutTimeoutsAndLimits.PollingRequestQueueTimeout, "Should have waited longer than the PollingRequestQueueTimeout");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task WhenThePollingRequestHasBegunTransfer_AndWeTimeoutWaitingForTheResponse_ThenRpcCallShouldFailWithTimeoutErrorMessage(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
            halibutTimeoutsAndLimits.TcpClientReceiveResponseTimeout = TimeSpan.FromSeconds(6);

            using var waitSemaphore = new SemaphoreSlim(0, 1);
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
                var exception = (await AssertException.Throws<HalibutClientException>(async () => await doSomeActionClient.ActionAsync(new(CancellationToken)))).And;
                exception.Message.Should().ContainAny(
                        "Unable to read data from the transport connection: Connection timed out.",
                        "Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond");
                exception.ConnectionState.Should().Be(ConnectionState.Unknown);
                stopwatch.Stop();

                stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15), "Should have timed out quickly");
                
                Wait.UntilActionSucceeds(() =>
                {
                    connectionsObserver.ConnectionClosedCount.Should().Be(1, "When we time out waiting for the response, then the connection should be closed");
                }, TimeSpan.FromSeconds(30), Logger, CancellationToken);
                

                waitSemaphore.Release();
            }
        }
    }
}
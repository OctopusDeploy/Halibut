using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests
{
    public class ReceiveResponseTimeoutTests : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false)]
        public async Task WhenRpcExecutionExceedsReceiveResponseTimeout_ThenInitialDataReadShouldTimeout(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimits
            {
                TcpClientReceiveResponseTimeout = TimeSpan.FromMilliseconds(100),
                // Ensure subsequent reads do not trigger timeouts
                TcpClientReceiveResponseTransmissionAfterInitialReadTimeout = TimeSpan.FromHours(1)
            };

            // Arrange
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                             .WithDoSomeActionService(() => Thread.Sleep(3000))
                             .Build(CancellationToken))
            {
                var doSomeActionClient = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionService>();

                // Act
                (await AssertionExtensions.Should(() => doSomeActionClient.ActionAsync()).ThrowAsync<HalibutClientException>())
                    .And.Message.Should().ContainAny(
                        "Connection timed out.",
                        "A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false)]
        public async Task WhenRpcExecutionIsWithinReceiveResponseTimeout_ButSubsequentDataIsDelayed_ThenTimeoutShouldOccur(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            // Arrange
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimits
            {
                // Ensure execution time does not trigger timeouts
                TcpClientReceiveResponseTimeout = TimeSpan.FromHours(1),
                TcpClientReceiveResponseTransmissionAfterInitialReadTimeout = TimeSpan.FromMilliseconds(100)
            };

            var enoughDataToCauseMultipleReadOperations = Enumerable.Range(0, 1024 * 1024)
                .Select(_ => Guid.NewGuid().ToString())
                .ToList();
            
            var listService = new AsyncListService(enoughDataToCauseMultipleReadOperations);
            
            var dataTransferObserver = new DataTransferObserverBuilder()
                .WithWritingDataObserver((_, _) =>
                {
                    if (listService.WasCalled)
                    {
                        //Sleep for < TcpClientReceiveResponseTimeout to pass initial data receipt, but > TcpClientReceiveResponseTransmissionAfterInitialReadTimeout for timeout.
                        Thread.Sleep(1000);
                    }
                })
                .Build();

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                             .WithAsyncService<IListService, IAsyncListService>(() => listService)
                             .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port)
                                 .WithDataObserver(() => new BiDirectionalDataTransferObserver(dataTransferObserver, dataTransferObserver))
                                 .Build())
                             .Build(CancellationToken))
            {
                var lastServiceClient = clientAndService.CreateAsyncClient<IListService, IAsyncClientListService>();

                // Act
                (await AssertionExtensions.Should(() => lastServiceClient.GetListAsync()).ThrowAsync<HalibutClientException>())
                    .And.Message.Should().ContainAny(
                        "Connection timed out.",
                        "A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond");
            }
        }
    }
}
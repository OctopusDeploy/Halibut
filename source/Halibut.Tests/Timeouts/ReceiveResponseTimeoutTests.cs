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
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Timeouts
{
    public class ReceiveResponseTimeoutTests : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenRpcExecutionExceedsReceiveResponseTimeout_ThenInitialDataReadShouldTimeout(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            // Arrange
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimits();
            halibutTimeoutsAndLimits.WithAllTcpTimeoutsTo(TimeSpan.FromHours(1));
            halibutTimeoutsAndLimits.TcpClientReceiveResponseTimeout = TimeSpan.FromMilliseconds(100);
            
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
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenRpcExecutionIsWithinReceiveResponseTimeout_ButSubsequentDataIsDelayed_ThenTimeoutShouldOccur(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            // Arrange
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimits();
            halibutTimeoutsAndLimits.WithAllTcpTimeoutsTo(TimeSpan.FromHours(1));
            halibutTimeoutsAndLimits.TcpClientTimeout = new SendReceiveTimeout(sendTimeout:TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(100));

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
                        Thread.Sleep(halibutTimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout);
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
                        "A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond",
                        "Unable to read data from the transport connection: Connection reset by peer");
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenRpcExecutionIsWithinReceiveResponseTimeout_ButDataStreamDataIsDelayed_ThenTimeoutShouldOccur(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimits();
            halibutTimeoutsAndLimits.WithAllTcpTimeoutsTo(TimeSpan.FromHours(1));
            halibutTimeoutsAndLimits.TcpClientTimeout = new SendReceiveTimeout(sendTimeout:TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(1000));

            var largeStringForDataStream = Some.RandomAsciiStringOfLength(1024 * 1024);

            // Arrange
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                             .WithReturnSomeDataStreamService(() => DataStreamUtil.From(
                                 firstSend: largeStringForDataStream,
                                 andThenRun: () => Thread.Sleep(4000),
                                 thenSend: largeStringForDataStream))
                             .Build(CancellationToken))
            {
                var returnDataStreamService = clientAndService.CreateAsyncClient<IReturnSomeDataStreamService, IAsyncClientReturnSomeDataStreamService>();

                // Act
                var e = (await AssertionExtensions.Should(() => returnDataStreamService.SomeDataStreamAsync()).ThrowAsync<HalibutClientException>()).And;
                Logger.Information(e, "The received expected exception, we were expecting one");
                e.Message.Should().ContainAny(
                        "Connection timed out.",
                        "A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond");
            }
        }
    }
}
using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.PortForwarding;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class MaximumAuthorizedTcpConnectionsFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testListening: false, testPolling: true, testWebSocket: false, testNetworkConditions: false)]
        public async Task WhenLimitIsExceededSubsequentConnectionsAreRejected(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var countingRetryPolicy = new CountingRetryPolicy();
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimits
                {
                    MaximumActiveTcpConnectionsPerPollingSubscription = 3
                })
                .WithServiceOpeningMultiplePollingConnections(5)
                .WithStandardServices()
                .WithPollingReconnectRetryPolicy(() => countingRetryPolicy)
                .RecordingClientLogs(out var clientLogs)
                .Build(CancellationToken);
            
            var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
            await echoService.SayHelloAsync("Hello");

            //we should have 2 log messages saying that a client exceeded the max number of authorized connections
            clientLogs.SelectMany(kvp => kvp.Value.GetLogs())
                .Select(l => l.FormattedMessage)
                .Where(msg => msg.Contains("has exceeded the maximum number of active connections"))
                .Should()
                .HaveCount(2);

            countingRetryPolicy.TryCount.Should().Be(5);
            countingRetryPolicy.SuccessCount.Should().Be(6); //each active connection has 2 success calls :shrug:
        }
    }
}

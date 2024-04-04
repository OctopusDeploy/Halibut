using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
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
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimits
                {
                    MaximumAuthorisedTcpConnectionsPerThumbprint = 3
                })
                .WithServiceOpeningMultiplePollingConnections(5)
                .WithStandardServices()
                .Build(CancellationToken);

            var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
            var result = await echoService.SayHelloAsync("Hello");
            result.Should().Be("Hello...");
        }
    }
}

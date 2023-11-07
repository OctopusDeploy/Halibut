using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class TrustFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testListening: true, testPolling: false, testWebSocket: false, testNetworkConditions: false)]
        public async Task NewRequestsCannotBeMadeByUntrustedClients(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithStandardServices()
                .Build(CancellationToken);

            var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
            var result = await echoService.SayHelloAsync("Hello");
            result.Should().Be("Hello...");

            // Trust no one
            clientAndService.Service!.TrustOnly(Array.Empty<string>());

            await AssertAsync.Throws<Exception>(async () => await echoService.SayHelloAsync("Hello again"));
        }
    }
}

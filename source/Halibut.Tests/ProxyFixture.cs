using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ProxyFixture : BaseTest
    {
        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testWebSocket: false)]
        // PollingOverWebSockets does not support (or use) ProxyDetails if provided.
        public async Task OctopusCanSendMessagesToTentacle_WithEchoService_AndAProxy(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithProxy()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();
                (await echo.SayHelloAsync("Deploy package A")).Should().Be("Deploy package A...");

                for (var i = 0; i < 5; i++)
                {
                    (await echo.SayHelloAsync($"Deploy package A {i}")).Should().Be($"Deploy package A {i}...");
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false, testWebSocket: false, 
            testAsyncAndSyncClients: false // TODO - ASYNC ME UP!
            // This doesn't work in async.
            )]
        // PollingOverWebSockets does not support (or use) ProxyDetails if provided.
        public async Task OctopusCanNotSendMessagesToTentacle_WithEchoService_AndABrokenProxy(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithProxy()
                       .Build(CancellationToken))
            {
                await clientAndService.HttpProxy!.StopAsync(CancellationToken.None);

                var echo = clientAndService.CreateClient<IEchoService>();
                Func<string> action = () => echo.SayHello("Deploy package A");
                action.Should().Throw<HalibutClientException>();
            }
        }
    }
}

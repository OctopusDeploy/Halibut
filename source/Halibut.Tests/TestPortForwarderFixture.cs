using System;
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
    public class TestPortForwarderFixture : BaseTest
    {
        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false, testAsyncAndSyncClients: true)]
        public async Task ClientCanSendMessagesToTentacle_WithEchoService_AndPortForwrding(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithPortForwarding(i => PortForwarderUtil.ForwardingToLocalPort(i).Build())
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
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false, testAsyncAndSyncClients: true)]
        public async Task ClientCanNotSendMessagesToTentacle_WithEchoService_AndBrokenPortForwarding(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithPortForwarding(i => PortForwarderUtil.ForwardingToLocalPort(i).Build())
                       .Build(CancellationToken))
            {
                clientAndService.PortForwarder!.EnterKillNewAndExistingConnectionsMode();

                var echo = clientAndServiceTestCase.ClientAndServiceTestVersion.IsPreviousClient() ? 
                                clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>() : 
                                clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>(serviceEndPoint =>
                                {
                                    serviceEndPoint.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
                                });

                await AssertAsync.Throws<HalibutClientException>(async () => await echo.SayHelloAsync("Deploy package A"));
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false, testWebSocket: false, testAsyncAndSyncClients: true)]
        // PollingOverWebSockets does not support (or use) ProxyDetails if provided.
        public async Task ClientCanSendMessagesToTentacle_WithEchoService_AndPortForwarding_AndProxy(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithPortForwarding(i => PortForwarderUtil.ForwardingToLocalPort(i).Build())
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
    }
}

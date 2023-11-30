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
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ClientCanSendMessagesToTentacle_WithEchoService_AndPortForwrding(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithPortForwarding(i => PortForwarderUtil.ForwardingToLocalPort(i).Build())
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                (await echo.SayHelloAsync("Deploy package A")).Should().Be("Deploy package A...");

                for (var i = 0; i < 5; i++)
                {
                    (await echo.SayHelloAsync($"Deploy package A {i}")).Should().Be($"Deploy package A {i}...");
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ClientCanNotSendMessagesToTentacle_WithEchoService_AndBrokenPortForwarding(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithPortForwarding(i => PortForwarderUtil.ForwardingToLocalPort(i).Build())
                       .WithPortForwarding(out var portForwarderRef)
                       .Build(CancellationToken))
            {
                portForwarderRef.Value.EnterKillNewAndExistingConnectionsMode();

                var echo = clientAndServiceTestCase.ClientAndServiceTestVersion.IsPreviousClient() ? 
                                clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>() : 
                                clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(serviceEndPoint =>
                                {
                                    serviceEndPoint.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
                                });

                await AssertException.Throws<HalibutClientException>(async () => await echo.SayHelloAsync("Deploy package A"));
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false, testWebSocket: false)]
        // PollingOverWebSockets does not support (or use) ProxyDetails if provided.
        public async Task ClientCanSendMessagesToTentacle_WithEchoService_AndPortForwarding_AndProxy(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithPortForwarding(i => PortForwarderUtil.ForwardingToLocalPort(i).Build())
                       .WithProxy()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                (await echo.SayHelloAsync("Deploy package A")).Should().Be("Deploy package A...");

                for (var i = 0; i < 5; i++)
                {
                    (await echo.SayHelloAsync($"Deploy package A {i}")).Should().Be($"Deploy package A {i}...");
                }
            }
        }
    }
}

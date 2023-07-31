using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
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
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithPortForwarding(i => PortForwarderUtil.ForwardingToLocalPort(i).Build())
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");

                for (var i = 0; i < 5; i++)
                {
                    echo.SayHello($"Deploy package A {i}").Should().Be($"Deploy package A {i}...");
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ClientCanNotSendMessagesToTentacle_WithEchoService_AndBrokenPortForwarding(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithPortForwarding(i => PortForwarderUtil.ForwardingToLocalPort(i).Build())
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                clientAndService.PortForwarder!.EnterKillNewAndExistingConnectionsMode();

                var echo = clientAndService.CreateClient<IEchoService>();
                Func<string> action = () => echo.SayHello("Deploy package A");
                action.Should().Throw<HalibutClientException>();
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false, testWebSocket: false)]
        // PollingOverWebSockets does not support (or use) ProxyDetails if provided.
        public async Task ClientCanSendMessagesToTentacle_WithEchoService_AndPortForwarding_AndProxy(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithPortForwarding(i => PortForwarderUtil.ForwardingToLocalPort(i).Build())
                       .WithProxy()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");

                for (var i = 0; i < 5; i++)
                {
                    echo.SayHello($"Deploy package A {i}").Should().Be($"Deploy package A {i}...");
                }
            }
        }
    }
}

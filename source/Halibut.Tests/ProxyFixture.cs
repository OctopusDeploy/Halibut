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
    public class ProxyFixture : BaseTest
    {
        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testWebSocket: false)]
        // PollingOverWebSockets does not support (or use) ProxyDetails if provided.
        public async Task ClientCanSendMessagesToService_WhenUsingAProxy(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithProxy()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                (await echo.SayHelloAsync("Hello")).Should().Be("Hello...");

                for (var i = 0; i < 5; i++)
                {
                    (await echo.SayHelloAsync($"Hello {i}")).Should().Be($"Hello {i}...");
                }
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false)]
        public async Task ClientTimesOutConnectingToAProxy_WhenTheProxyIsUnavailable(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithHalibutLoggingLevel(LogLevel.Trace)
                       .WithStandardServices()
                       .WithProxy()
                       .Build(CancellationToken))
            {
                clientAndService.HttpProxy!.Dispose();

                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.TcpClientConnectTimeout = TimeSpan.FromSeconds(5);
                    point.RetryCountLimit = 2;
                    point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(10);
                });

                (await AssertException.Throws<HalibutClientException>(() => echo.SayHelloAsync("Hello")))
                    .And.Message.Should().ContainAny(
                        "No connection could be made because the target machine actively refused it",
                        "the polling endpoint did not collect the request within the allowed time",
                        "Connection refused");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false)]
        public async Task ClientTimesOutConnectingToAProxy_WhenTheProxyHangsDuringConnect(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithHalibutLoggingLevel(LogLevel.Trace)
                       .WithStandardServices()
                       .WithProxy()
                       .Build(CancellationToken))
            {
                clientAndService.HttpProxy!.PauseNewConnections();

                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.TcpClientConnectTimeout = TimeSpan.FromSeconds(5);
                    point.RetryCountLimit = 2;
                    point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(10);
                });

                var exception = (await AssertException.Throws<HalibutClientException>(() => echo.SayHelloAsync("Hello"))).And;
                Logger.Information(exception, "Got exception, we were expecting one.");    
                exception.Message.Should().ContainAny(
                        "No connection could be made because the target machine actively refused it",
                        "the polling endpoint did not collect the request within the allowed time",
                        "Unable to read data from the transport connection: Connection timed out.",
                        "A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.",
                        "A timeout while waiting for the proxy server at");
                ;
            }
        }
    }
}

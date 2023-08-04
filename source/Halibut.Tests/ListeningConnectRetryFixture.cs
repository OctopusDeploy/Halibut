using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Tests.Support;
using Halibut.Tests.Support.PortForwarding;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ListeningConnectRetryFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling:false, testAsyncAndSyncClients: true)]
        public async Task ListeningRetriesAttemptsUpToTheConfiguredValue(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            TcpConnectionsCreatedCounter tcpConnectionsCreatedCounter = null;
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(port =>
                       {
                           var portForwarder = PortForwarderUtil.ForwardingToLocalPort(port)
                               .WithCountTcpConnectionsCreated(out tcpConnectionsCreatedCounter)
                               .Build();

                           return portForwarder;
                       })
                       .WithEchoService()
                       .WithHalibutLoggingLevel(LogLevel.Fatal)
                       .Build(CancellationToken))
            {
                clientAndService.PortForwarder!.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.Zero;
                    point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    point.RetryCountLimit = 20;
                });
                
                await AssertAsync.Throws<HalibutClientException>(() => echoService.SayHelloAsync("hello"));

                tcpConnectionsCreatedCounter.ConnectionsCreatedCount.Should().Be(20);
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false, testAsyncAndSyncClients: true)]
        public async Task ListeningRetriesAttemptsUpToTheConfiguredTimeout(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            TcpConnectionsCreatedCounter tcpConnectionsCreatedCounter = null;
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(port =>
                       {
                           var portForwarder = PortForwarderUtil.ForwardingToLocalPort(port)
                               .WithCountTcpConnectionsCreated(out tcpConnectionsCreatedCounter)
                               .Build();

                           return portForwarder;
                       })
                       .WithEchoService()
                       .WithHalibutLoggingLevel(LogLevel.Fatal)
                       .Build(CancellationToken))
            {
                clientAndService.PortForwarder!.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.FromSeconds(1);
                    point.ConnectionErrorRetryTimeout = TimeSpan.FromSeconds(15);
                    point.RetryCountLimit = 100000000;
                });

                var sw = Stopwatch.StartNew();
                await AssertAsync.Throws<HalibutClientException>(() => echoService.SayHelloAsync("hello"));
                sw.Stop();

                
                sw.Elapsed.Should().BeCloseTo(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(7)/* Give a big amount of leeway */);
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false, testAsyncAndSyncClients: true)]
        public async Task ListeningRetryListeningSleepIntervalWorks(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            TcpConnectionsCreatedCounter tcpConnectionsCreatedCounter = null;
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(port =>
                       {
                           var portForwarder = PortForwarderUtil.ForwardingToLocalPort(port)
                               .WithCountTcpConnectionsCreated(out tcpConnectionsCreatedCounter)
                               .Build();

                           return portForwarder;
                       })
                       .WithEchoService()
                       .WithHalibutLoggingLevel(LogLevel.Fatal)
                       .Build(CancellationToken))
            {
                clientAndService.PortForwarder!.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.FromSeconds(10);
                    point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    point.RetryCountLimit = 4;
                });

                var sw = Stopwatch.StartNew();
                await AssertAsync.Throws<HalibutClientException>(() => echoService.SayHelloAsync("hello"));
                sw.Stop();

                // Expected ~30s since we sleep 10s _between_ each attempt.
                sw.Elapsed.Should().BeCloseTo(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(15)/* Give a big amount of leeway for windows */);
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false, testAsyncAndSyncClients: true)]
        public async Task ListeningRetriesAttemptsCanEventuallyWork(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            TcpConnectionsCreatedCounter tcpConnectionsCreatedCounter = null;
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(port =>
                       {
                           var portForwarder = PortForwarderUtil.ForwardingToLocalPort(port)
                               .WithCountTcpConnectionsCreated(out tcpConnectionsCreatedCounter)
                               .Build();

                           return portForwarder;
                       })
                       .WithEchoService()
                       .WithHalibutLoggingLevel(LogLevel.Fatal)
                       .Build(CancellationToken))
            {
                clientAndService.PortForwarder!.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.FromSeconds(1);
                    point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    point.RetryCountLimit = 999999;
                });

                var echoCallThatShouldEventuallySucceed = Task.Run(() => echoService.SayHelloAsync("hello"));
                while (tcpConnectionsCreatedCounter.ConnectionsCreatedCount < 5)
                {
                    Logger.Information("TCP count is at: {Count}", tcpConnectionsCreatedCounter.ConnectionsCreatedCount);
                    await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken);
                }
                clientAndService.PortForwarder.ReturnToNormalMode();

                await echoCallThatShouldEventuallySucceed;
            }
        }
    }
}

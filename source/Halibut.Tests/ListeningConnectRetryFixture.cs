using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
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
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling:false)]
        public async Task ListeningRetriesAttemptsUpToTheConfiguredValue(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            TcpConnectionsCreatedCounter? tcpConnectionsCreatedCounter = null;
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(out var portForwarderRef, port =>
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
                portForwarderRef.Value.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.Zero;
                    point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    point.RetryCountLimit = 20;
                });
                
                await AssertException.Throws<HalibutClientException>(() => echoService.SayHelloAsync("hello"));

                tcpConnectionsCreatedCounter!.ConnectionsCreatedCount.Should().Be(20);
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false)]
        public async Task ListeningRetriesAttemptsUpToTheConfiguredTimeout(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(out var portForwarderRef, port =>
                       {
                           var portForwarder = PortForwarderUtil.ForwardingToLocalPort(port)
                               .WithCountTcpConnectionsCreated(out _)
                               .Build();

                           return portForwarder;
                       })
                       .WithEchoService()
                       .WithHalibutLoggingLevel(LogLevel.Fatal)
                       .Build(CancellationToken))
            {
                portForwarderRef.Value.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.FromSeconds(1);
                    point.ConnectionErrorRetryTimeout = TimeSpan.FromSeconds(15);
                    point.RetryCountLimit = 100000000;
                });

                var sw = Stopwatch.StartNew();
                await AssertException.Throws<HalibutClientException>(() => echoService.SayHelloAsync("hello"));
                sw.Stop();

                
                sw.Elapsed.Should().BeCloseTo(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(7)/* Give a big amount of leeway */);
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false)]
        public async Task ListeningRetryListeningSleepIntervalWorks(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(out var portForwarderRef, port =>
                       {
                           var portForwarder = PortForwarderUtil.ForwardingToLocalPort(port)
                               .WithCountTcpConnectionsCreated(out _)
                               .Build();

                           return portForwarder;
                       })
                       .WithEchoService()
                       .WithHalibutLoggingLevel(LogLevel.Fatal)
                       .Build(CancellationToken))
            {
                portForwarderRef.Value.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.FromSeconds(10);
                    point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    point.RetryCountLimit = 4;
                });

                var sw = Stopwatch.StartNew();
                await AssertException.Throws<HalibutClientException>(() => echoService.SayHelloAsync("hello"));
                sw.Stop();

                // Expected ~30s since we sleep 10s _between_ each attempt.
                sw.Elapsed.Should().BeCloseTo(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(15)/* Give a big amount of leeway for windows */);
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false)]
        public async Task ConnectionErrorRetryTimeout_IsAHardDeadlineEvenWhenIndividualAttemptsBlockLonger(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            // Each attempt hangs for perAttemptTimeout while the SSL handshake waits for data
            // that the paused port forwarder will never deliver.
            // ConnectionErrorRetryTimeout is shorter and should act as the hard deadline.
            var perAttemptTimeout = TimeSpan.FromSeconds(8);
            var connectionErrorRetryTimeout = TimeSpan.FromSeconds(3);

            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.TcpClientTimeout = new SendReceiveTimeout(
                sendTimeout: perAttemptTimeout,
                receiveTimeout: perAttemptTimeout);
            halibutTimeoutsAndLimits.TcpClientConnectTimeout = TimeSpan.FromSeconds(60);
            halibutTimeoutsAndLimits.ConnectionErrorRetryTimeout = connectionErrorRetryTimeout;
            halibutTimeoutsAndLimits.RetryCountLimit = int.MaxValue;

            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .As<LatestClientAndLatestServiceBuilder>()
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .WithPortForwarding(out var portForwarder)
                .WithEchoService()
                .WithHalibutLoggingLevel(LogLevel.Fatal)
                .Build(CancellationToken);

            // Pause mode: TCP connects immediately but data is frozen, so the SSL handshake
            // stalls until the stream read timeout (perAttemptTimeout) fires.
            portForwarder.Value.EnterPauseNewAndExistingConnectionsMode();

            var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

            var sw = Stopwatch.StartNew();
            await AssertException.Throws<HalibutClientException>(() => echoService.SayHelloAsync("hello"));
            sw.Stop();

            // Should bail out around connectionErrorRetryTimeout (3s), not perAttemptTimeout (8s).
            // Currently FAILS: the retry loop only checks ConnectionErrorRetryTimeout between
            // attempts, so the first attempt runs to completion at ~8s before the loop can exit.
            sw.Elapsed.Should().BeLessThan(perAttemptTimeout - TimeSpan.FromSeconds(2),
                because: $"ConnectionErrorRetryTimeout ({connectionErrorRetryTimeout}) should bound the total wait, not the per-attempt timeout ({perAttemptTimeout})");
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling: false)]
        public async Task ListeningRetriesAttemptsCanEventuallyWork(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            TcpConnectionsCreatedCounter? tcpConnectionsCreatedCounter = null;
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(out var portForwarderRef, port =>
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
                portForwarderRef.Value.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.FromSeconds(1);
                    point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    point.RetryCountLimit = 999999;
                });

                var echoCallThatShouldEventuallySucceed = Task.Run(() => echoService.SayHelloAsync("hello"));
                while (tcpConnectionsCreatedCounter!.ConnectionsCreatedCount < 5)
                {
                    Logger.Information("TCP count is at: {Count}", tcpConnectionsCreatedCounter.ConnectionsCreatedCount);
                    await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken);
                }
                portForwarderRef.Value.ReturnToNormalMode();

                await echoCallThatShouldEventuallySucceed;
            }
        }
    }
}

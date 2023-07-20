using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.PortForwarding;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ListeningConnectRetryFixture : BaseTest
    {
        [Test]
        public async Task ListeningRetriesAttemptsUpToTheConfiguredValue()
        {
            TcpConnectionsCreatedCounter tcpConnectionsCreatedCounter = null;
            using (var clientAndService = await LatestClientAndLatestServiceBuilder.Listening()
                       .WithPortForwarding(port =>
                       {
                           var portForwarder = PortForwarderUtil.ForwardingToLocalPort(port)
                               .WithCountTcpConnectionsCreated(out tcpConnectionsCreatedCounter)
                               .Build();

                           return portForwarder;
                       })
                       .WithEchoService()
                       .Build(CancellationToken))
            {
                clientAndService.PortForwarder!.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateClient<IEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.Zero;
                    point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    point.RetryCountLimit = 100;
                });
                
                Assert.Throws<HalibutClientException>(() => echoService.SayHello("hello"));

                tcpConnectionsCreatedCounter.ConnectionsCreatedCount.Should().Be(100);
            }
        }
        
        [Test]
        public async Task ListeningRetriesAttemptsUpToTheConfiguredTimeout()
        {
            TcpConnectionsCreatedCounter tcpConnectionsCreatedCounter = null;
            using (var clientAndService = await LatestClientAndLatestServiceBuilder.Listening()
                       .WithPortForwarding(port =>
                       {
                           var portForwarder = PortForwarderUtil.ForwardingToLocalPort(port)
                               .WithCountTcpConnectionsCreated(out tcpConnectionsCreatedCounter)
                               .Build();

                           return portForwarder;
                       })
                       .WithEchoService()
                       .Build(CancellationToken))
            {
                clientAndService.PortForwarder!.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateClient<IEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.FromSeconds(1);
                    point.ConnectionErrorRetryTimeout = TimeSpan.FromSeconds(15);
                    point.RetryCountLimit = 100000000;
                });

                var sw = Stopwatch.StartNew();
                Assert.Throws<HalibutClientException>(() => echoService.SayHello("hello"));
                sw.Stop();

                
                sw.Elapsed.Should().BeCloseTo(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(7)/* Give a big amount of leeway */);
            }
        }
        
        [Test]
        public async Task ListeningRetryListeningSleepIntervalWorks()
        {
            TcpConnectionsCreatedCounter tcpConnectionsCreatedCounter = null;
            using (var clientAndService = await LatestClientAndLatestServiceBuilder.Listening()
                       .WithPortForwarding(port =>
                       {
                           var portForwarder = PortForwarderUtil.ForwardingToLocalPort(port)
                               .WithCountTcpConnectionsCreated(out tcpConnectionsCreatedCounter)
                               .Build();

                           return portForwarder;
                       })
                       .WithEchoService()
                       .Build(CancellationToken))
            {
                clientAndService.PortForwarder!.EnterKillNewAndExistingConnectionsMode();

                var echoService = clientAndService.CreateClient<IEchoService>(point =>
                {
                    point.RetryListeningSleepInterval = TimeSpan.FromSeconds(1);
                    point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    point.RetryCountLimit = 15;
                });

                var sw = Stopwatch.StartNew();
                Assert.Throws<HalibutClientException>(() => echoService.SayHello("hello"));
                sw.Stop();

                
                sw.Elapsed.Should().BeCloseTo(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(7)/* Give a big amount of leeway */);
            }
        }
    }
}
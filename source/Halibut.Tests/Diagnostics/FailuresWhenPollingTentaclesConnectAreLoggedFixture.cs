using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Streams;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Streams;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Diagnostics
{
    public class FailuresWhenPollingTentaclesConnectAreLoggedFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testListening: false, testNetworkConditions: false)]
        public async Task VeryEarlyOnFailuresAreRecordedAsInitialisationErrors(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .RecordingClientLogs(out var clientLogs)
                             .WithPortForwarding(out var portForwarder)
                             .WithClientStreamFactory(new ActionBeforeCreateStreamFactory(new StreamFactory(clientAndServiceTestCase.ServiceAsyncHalibutFeature),
                                 () => { portForwarder.Value.CloseExistingConnections(); }))
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();
                // If this task completes and then we likely didn't kill the connect as we intended to.  
                var checkPollingTentacleDidntConnect = Task.Run(async () => await echo.SayHelloAsync("Deploy package A"));

                await Wait.For(async () =>
                {
                    await Task.CompletedTask;
                    var logs = clientLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                    if (logs.Any(l => l.Type == EventType.ErrorInInitialisation)) return true;
                    return checkPollingTentacleDidntConnect.IsCompleted;
                }, CancellationToken);

                checkPollingTentacleDidntConnect.IsCompleted.Should().BeFalse("We should have killed the connection before the request");

                var logs = clientLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                logs.Should().Match(logs => logs.Any(l => l.Type == EventType.ErrorInInitialisation));
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testListening: false,
            testWebSocket: false, // Web Sockets do init work to go from TCP to ssl to http finally to web socket before we
            // get to it so killing the connection via the port forwarder does not result in a connection
            // error we can see.
            testNetworkConditions: false)]
        public async Task VeryEarlyOnFailuresAreRecordedAsInitialisationErrors_KilledAfterFirstWrite(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .RecordingClientLogs(out var clientLogs)
                             .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port)
                                 .WithDataObserver(() =>
                                 {
                                     var connectionKiller = new DataTransferObserverBuilder()
                                         .WithKillConnectionAfterANumberOfWrites(Logger, 2)
                                         .Build();
                                     return new BiDirectionalDataTransferObserver(connectionKiller, connectionKiller);
                                 })
                                 .Build())
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();
                // If this task completes and then we likely didn't kill the connect as we intended to.  
                var checkPollingTentacleDidntConnect = Task.Run(async () => await echo.SayHelloAsync("Deploy package A"));

                await Wait.For(async () =>
                {
                    await Task.CompletedTask;
                    var logs = clientLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                    if (logs.Any(l => l.Type == EventType.ErrorInInitialisation)) return true;
                    return checkPollingTentacleDidntConnect.IsCompleted;
                }, CancellationToken);

                checkPollingTentacleDidntConnect.IsCompleted.Should().BeFalse("We should have killed the connection before the request");

                var logs = clientLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                logs.Should().Match(logs => logs.Any(l => l.Type == EventType.ErrorInInitialisation));
            }
        }
    }
}
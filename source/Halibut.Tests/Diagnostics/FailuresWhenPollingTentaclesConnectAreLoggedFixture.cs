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
                             .WithClientStreamFactory(new ActionBeforeCreateStreamFactory(new StreamFactory(),
                                 () => { portForwarder.Value.CloseExistingConnections(); }))
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                // If this task completes and then we likely didn't kill the connect as we intended to.  
                var checkPollingTentacleDidNotConnect = Task.Run(async () => await echo.SayHelloAsync("Deploy package A"));


                await Wait.For(async () =>
                {
                    await Task.CompletedTask;
                    var logs = clientLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                    if (logs.Any(IsListeningIntialisationFailureLogMessage)) return true;
                    return checkPollingTentacleDidNotConnect.IsCompleted;
                }, CancellationToken);

                checkPollingTentacleDidNotConnect.IsCompleted.Should().BeFalse("We should have killed the connection before the request");
                
                var logs = clientLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                logs.Should().Match(logs => logs.Any(IsListeningIntialisationFailureLogMessage));
            }
        }

        static bool IsListeningIntialisationFailureLogMessage(LogEvent l)
        {
            return l.Type == EventType.ErrorInInitialisation
                   // Also check for cases where it looks like the client just walked away, maybe nmap scanned us?
                   || (l.Type == EventType.Diagnostic && l.FormattedMessage.Contains("did not complete the TLS handshake"));
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
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                // If this task completes and then we likely didn't kill the connect as we intended to.  
                var checkPollingTentacleDidNotConnect = Task.Run(async () => await echo.SayHelloAsync("Deploy package A"));

                await Wait.For(async () =>
                {
                    await Task.CompletedTask;
                    var logs = clientLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                    if (logs.Any(IsListeningIntialisationFailureLogMessage)) return true;
                    return checkPollingTentacleDidNotConnect.IsCompleted;
                }, CancellationToken);

                checkPollingTentacleDidNotConnect.IsCompleted.Should().BeFalse("We should have killed the connection before the request");
                
                var logs = clientLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                logs.Should().Match(logs => logs.Any(IsListeningIntialisationFailureLogMessage));
            }
        }
    }
}
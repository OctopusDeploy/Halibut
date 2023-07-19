using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.PortForwarding;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Timeouts
{
    public class NextAndProceedControlMessageTimeoutsFixture : BaseTest
    {
        [Test]
        public async Task PauseOnPollingTentacleSendingNextControlMessage_ShouldNotHangForEver()
        {
            var dataSentSizes = new List<long>();
            long? pauseStreamWhenServiceSendsMessageOfSize = null;
            var serviceConnectionType = ServiceConnectionType.Polling;
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port)
                           .WithPortForwarderDataLogging(serviceConnectionType)
                           .WithPortForwarderServiceSentDataObserver(serviceConnectionType, (tcpPump, stream) =>
                           {
                               dataSentSizes.Add(stream.Length);
                               if (pauseStreamWhenServiceSendsMessageOfSize != null && pauseStreamWhenServiceSendsMessageOfSize == stream.Length)
                               {
                                   Logger.Information("Pausing pump since a write of size {Size} was received", stream.Length);
                                   tcpPump.Pause();
                                   pauseStreamWhenServiceSendsMessageOfSize = null;
                               }
                           })
                           .Build())
                       .WithEchoService()
                       .WithPollingReconnectRetryPolicy(() => new RetryPolicy(1, TimeSpan.Zero, TimeSpan.Zero))
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                
                echo.SayHello(Some.RandomAsciiStringOfLength(2000));
                // --> NEXT sent
                // --> Proceed sent
                await Task.Delay(TimeSpan.FromSeconds(3)); // Allow enough time for the polling queue to unnecessarily send a null message down the queue because of a race condition in the queue. 
                long nextMessageSize = dataSentSizes.Last();
                pauseStreamWhenServiceSendsMessageOfSize = nextMessageSize; // Lets hope they are all the same size
                Logger.Information("Will pause the pump next time the polling service sends a message of size {Size}", pauseStreamWhenServiceSendsMessageOfSize);

                echo.SayHello(Some.RandomAsciiStringOfLength(2000)); // Second last message
                // --> NEXT sent and pump paused here.
                
                var sw = Stopwatch.StartNew();
                // Service must detect that it can't get a PROCEED back in time and so must decide to abandon and then try again within
                // its retry policy.
                echo.SayHello(Some.RandomAsciiStringOfLength(2000));
                sw.Stop();
                Logger.Information("It took: " + sw.Elapsed);
                sw.Elapsed.Should().BeGreaterThanOrEqualTo(HalibutLimits.TcpClientHeartbeatReceiveTimeout - TimeSpan.FromSeconds(2)) // -2s since tentacle will begin its countdown on the read which may start just after
                                                                                                                                     // the response to the 'Second last message' is put back into the queue.
                    .And.BeLessThan(HalibutLimits.TcpClientReceiveTimeout, "Service should not be using this timeout to detect the control message is not coming back, it should use the shorter one.");
            }
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Timeouts
{
    public class PollingQueueTimeouts : BaseTest
    {
        [Test]
        public async Task WhenNoMessagesAreSentToAPollingTentacle_ThePollingRequestQueueCausesNullMessagesToBeSent_KeepingTheConnectionAlive()
        {
            var serviceConnectionType = ServiceConnectionType.Polling;
            var timeSpansBetweenDataFlowing = new ConcurrentBag<TimeSpan>();
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port)
                           .WithDataObserver(() =>
                           {
                               var perTcpPumpStopwatch = Stopwatch.StartNew();
                               return new BiDirectionalDataTransferObserver(
                                   new DataTransferObserverBuilder()
                                       .WithWritingDataObserver((tcpPump, Stream) =>
                                       {
                                           timeSpansBetweenDataFlowing.Add(perTcpPumpStopwatch.Elapsed);
                                           perTcpPumpStopwatch.Reset();
                                       })
                                       .Build(),
                                   new DataTransferObserverBuilder().Build());
                           })
                           .Build())
                       .WithPendingRequestQueueFactory(logFactory => new FuncPendingRequestQueueFactory(uri => new PendingRequestQueue(logFactory.ForEndpoint(uri), TimeSpan.FromSeconds(1))))
                       .WithEchoService()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                timeSpansBetweenDataFlowing = new ConcurrentBag<TimeSpan>();

                await Task.Delay(TimeSpan.FromSeconds(20));

                timeSpansBetweenDataFlowing.Count.Should().BeGreaterThan(5, "Even though we are not sending messages over the wire, the polling queue should be sending null messages keeping the connection alive.");
                foreach (var timeSpan in timeSpansBetweenDataFlowing)
                {
                    timeSpan.Should().BeLessThan(TimeSpan.FromSeconds(6), "The polling queue should be timing out causing messages to flow across the wire, this should be happen close to every 1 second.");
                }
            }
        }
    }
}
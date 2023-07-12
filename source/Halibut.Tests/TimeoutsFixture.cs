using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class TimeoutsFixture
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task ReadingARequestMessage(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithPortForwarding(out var portForwarderRef)
                       .WithEchoService()
                       .WithDoSomeActionService(() => portForwarderRef.Value.PauseExistingConnections())
                       .Build())
            {
                portForwarderRef.Value = clientAndService.PortForwarder;
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                var pauseConnections = clientAndService.CreateClient<IDoSomeActionService>(point =>
                {
                    // We don't want to measure the polling queue timeouts.
                    point.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromMinutes(10);
                    point.PollingRequestQueueTimeout = TimeSpan.FromMinutes(10);
                });

                var sw = Stopwatch.StartNew();
                var e = Assert.Throws<HalibutClientException>(() => pauseConnections.Action());
                sw.Stop();
                new SerilogLoggerBuilder().Build().Error(e, "msg");
                sw.Elapsed.Should().BeCloseTo(HalibutLimits.TcpClientReceiveTimeout, TimeSpan.FromSeconds(5));
            }
        }
    }
}
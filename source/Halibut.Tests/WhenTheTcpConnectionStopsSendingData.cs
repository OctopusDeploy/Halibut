using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenTheTcpConnectionStopsSendingData : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket:false, testPolling:false)]
        public async Task HalibutCanRecoverFromIdleTcpDisconnect(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithEchoService()
                       .WithPortForwarding()
                       .Build(CancellationToken))
            {
                var data = new byte[1024];
                new Random().NextBytes(data);

                var echo = clientAndService.CreateClient<IEchoService>();

                echo.SayHello("Bob");

                clientAndService.PortForwarder!.PauseExistingConnections();

                var sw = Stopwatch.StartNew();
                echo.SayHello("Bob");
                sw.Stop();

                sw.Elapsed.Should().BeGreaterThanOrEqualTo(HalibutLimits.TcpClientHeartbeatReceiveTimeout - TimeSpan.FromSeconds(1), // Allow for some slack, don't care if it actually waited just under.  
                    "Since we should test connections in the pool using using the heart beat timeout.")
                    .And.BeLessThan(HalibutLimits.TcpClientReceiveTimeout, "Since we should test connections in the pool using using the shorter timeout.");
            }
        }
    }
}

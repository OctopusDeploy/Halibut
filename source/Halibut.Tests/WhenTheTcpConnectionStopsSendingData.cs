using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenTheTcpConnectionStopsSendingData
    {
        [Test]
        public async Task HalibutCanRecoverFromIdleTcpDisconnect2()
        {
            using (var clientAndService = ClientServiceBuilder
                       .Listening()
                       .WithEchoService()
                       .WithPortForwarding()
                       .Build())
            {
                var data = new byte[1024];
                new Random().NextBytes(data);

                var echo = clientAndService.CreateClient<IEchoService>();

                echo.SayHello("Bob");

                clientAndService.PortForwarder!.PauseExistingConnections();

                var sayHelloTask = Task.Run(() => echo.SayHello("Bob"));

                // The test knows that Halibut should be using the shorter TcpClientHeartbeatReceiveTimeout when checking
                // the TCP connection pulled out of the pool. Doing this reduces the test time in the failure case.
                await Task.WhenAny(sayHelloTask, Task.Delay(HalibutLimits.TcpClientHeartbeatReceiveTimeout + HalibutLimits.TcpClientHeartbeatReceiveTimeout));

                sayHelloTask.IsCompleted.Should().BeTrue("We should be able to detect dead TCP connections and retry requests with a new TCP connection.");

                (HalibutLimits.TcpClientHeartbeatReceiveTimeout + TimeSpan.FromSeconds(10)).Should().BeLessThan(HalibutLimits.TcpClientReceiveTimeout, 
                    "This depend on the heart beat timeouts being less than the regular TCP timeouts, if that is not true this test isn't testing the correct timeout.");
            }
        }
    }
}
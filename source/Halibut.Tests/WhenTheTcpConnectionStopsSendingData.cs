using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests
{
    public class WhenTheTcpConnectionStopsSendingData
    {
        [Test]
        public async Task HalibutCanRecoverFromIdleTcpDisconnect2()
        {
            using (var clientAndService = ClientServiceBuilder.Listening()
                       .WithService<IEchoService>(() => new EchoService())
                       .WithPortForwarding()
                       .Build())
            {
                var data = new byte[1024];
                new Random().NextBytes(data);

                var echo = clientAndService.CreateClient<IEchoService>();

                echo.SayHello("Bob");

                clientAndService.portForwarder.PauseExistingConnections();

                var sayHelloTask = Task.Run(() => echo.SayHello("Bob"));

                // The test knows that Halibut should be using the shorter TcpClientHeartbeatReceiveTimeout when checking
                // the TCP connection pulled out of the pool. Doing this reduces the test time in the failure case.
                await Task.WhenAny(sayHelloTask, Task.Delay(HalibutLimits.TcpClientHeartbeatReceiveTimeout + HalibutLimits.TcpClientHeartbeatReceiveTimeout));

                sayHelloTask.IsCompleted.Should().BeTrue("We should be able to detect dead TCP connections and retry requests with a new TCP connection.");
            }
        }
    }
}
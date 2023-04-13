using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenTheTcpConnectionStopsSendingData
    {
        [Test]
        public async Task HalibutCanRecoverFromIdleTcpDisconnect()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());

            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                using (var portForwarder = new PortForwarder(new Uri("https://localhost:" + tentaclePort), TimeSpan.Zero))
                {
                    tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                    var data = new byte[1024];
                    new Random().NextBytes(data);

                    var echo = octopus.CreateClient<IEchoService>("https://localhost:" + portForwarder.PublicEndpoint.Port, Certificates.TentacleListeningPublicThumbprint);

                    echo.SayHello("Bob");

                    portForwarder.PauseExistingConnections();

                    var sayHelloTask = Task.Run(() => echo.SayHello("Bob"));

                    // The test knows that Halibut should be using the shorter TcpClientHeartbeatReceiveTimeout when checking
                    // the TCP connection pulled out of the pool. Doing this reduces the test time in the failure case.
                    await Task.WhenAny(sayHelloTask, Task.Delay(HalibutLimits.TcpClientHeartbeatReceiveTimeout + HalibutLimits.TcpClientHeartbeatReceiveTimeout));

                    sayHelloTask.IsCompleted.Should().BeTrue("We should be able to detect dead TCP connections and retry requests with a new TCP connection.");
                }
            }
        }
    }
}
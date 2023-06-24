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
        
        [Test]
        public async Task WhileTheServiceIsProcessingAMessage_TheClientShouldNotWaitForEver()
        {
            var services = new DelegateServiceFactory();
            var callBackService = new CallBackService();
            services.Register<ICallBackService>(() => callBackService);
            var halibutTimeouts = new HalibutTimeouts();
            halibutTimeouts.TcpClientReceiveTimeout = TimeSpan.FromSeconds(10);
            
            using (var octopus = new HalibutRuntimeBuilder()
                       .WithServerCertificate(Certificates.Octopus)
                       .WithHalibutTimeouts(halibutTimeouts)
                       .Build())
            using (var tentacleListening = new HalibutRuntimeBuilder()
                       .WithServerCertificate(Certificates.TentacleListening)
                       .WithServiceFactory(services)
                       .Build())
            {
                var tentaclePort = tentacleListening.Listen();
                using (var loadBalancer = new PortForwarder(new Uri("https://localhost:" + tentaclePort), TimeSpan.Zero))
                {
                    tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                    var serviceEndppoint = new ServiceEndPoint("https://localhost:" + loadBalancer.PublicEndpoint.Port,  Certificates.TentacleListeningPublicThumbprint);
                    
                    var remoteCallBack = octopus.CreateClient<ICallBackService>("https://localhost:" + loadBalancer.PublicEndpoint.Port, Certificates.TentacleListeningPublicThumbprint);

                    // This should be a no-op
                    remoteCallBack.MakeTheCall();

                    callBackService.CallBack = () =>
                    {
                        foreach (var portForwarder in loadBalancer.Pumps)
                        {
                            portForwarder.Pause();
                        }
                    };



                    var makeTheCallWhenTheConnectionWillPause = Task.Run(() => remoteCallBack.MakeTheCall());

                    // The test knows that Halibut should be using the shorter TcpClientHeartbeatReceiveTimeout when checking
                    // the TCP connection pulled out of the pool. Doing this reduces the test time in the failure case.
                    await Task.WhenAny(makeTheCallWhenTheConnectionWillPause, Task.Delay(TimeSpan.FromSeconds(20)));

                    makeTheCallWhenTheConnectionWillPause.IsCompleted.Should().BeTrue("The caller should not have waited forever.");
                }
            }
        }
    }
}
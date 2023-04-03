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
    public class WhenTheTcpConnectionIsKilledWhileWaitingForTheResponse
    {
        [Test]
        public async Task ToPolling_AResponseShouldBeQuicklyReturned()
        {
            var services = new DelegateServiceFactory();
            DoSomeActionService doSomeActionService = new DoSomeActionService();
            services.Register<IDoSomeActionService>(() => doSomeActionService);
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            {
                var octopusPort = octopus.Listen();
                using (var portForwarder = new PortForwarder(new Uri("https://localhost:" + octopusPort), TimeSpan.Zero))
                using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                {
                        
                    octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                    tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + portForwarder.PublicEndpoint.Port), Certificates.OctopusPublicThumbprint));

                    var svc = octopus.CreateClient<IDoSomeActionService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                    doSomeActionService.ActionDelegate = () => portForwarder.Dispose();
                    
                    // When svc.Action() is executed, tentacle will kill the TCP connection and dispose the port forwarder preventing new connections.
                    var killPortForwarderTask = Task.Run(() => svc.Action());
                    
                    await Task.WhenAny(killPortForwarderTask, Task.Delay(TimeSpan.FromSeconds(10)));
                    
                    killPortForwarderTask.Status.Should().Be(TaskStatus.Faulted, "We should immediately get an error response.");
                }
            }
        }
    }
}
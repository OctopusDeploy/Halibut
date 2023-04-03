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
    public class WhenTheTCPConnectionIsKilledMidRequest
    {
        [Test]
        public async Task HalibutCanRecoverFromIdleTcpDisconnect()
        {
            var services = new DelegateServiceFactory();
            var tcpKiller = new TcpKiller();
            services.Register<IKillTheTCPConnection>(() => tcpKiller);
    
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                using (var loadBalancer = new PortForwarder(new Uri("https://localhost:" + tentaclePort), TimeSpan.Zero))
                {
                    tcpKiller.portForwarder = loadBalancer;
                    tentacleListening.Trust(Certificates.OctopusPublicThumbprint);
                
                    var data = new byte[1024];
                    new Random().NextBytes(data);

                    var serviceEndpoint = new ServiceEndPoint("https://localhost:" + loadBalancer.PublicEndpoint.Port, Certificates.TentacleListeningPublicThumbprint);
                    
                    
                    var echo = octopus.CreateClient<IKillTheTCPConnection>(serviceEndpoint);

                    echo.AndKillIt();

                    int count = echo.CallCount();

                    count.Should().Be(2);
                }
            }
        }
        
        
        public interface IKillTheTCPConnection
        {
            void AndKillIt();

            int CallCount();
        }
        
        public class TcpKiller : IKillTheTCPConnection
        {
            public PortForwarder portForwarder { get; set; }

            int callCounter = 0;
            public TcpKiller()
            {
            }

            public void AndKillIt()
            {
                callCounter++;
                foreach (var portForwarder in portForwarder.Pumps)
                {
                    portForwarder.Pause();
                }
            }

            public int CallCount()
            {
                callCounter++;
                return callCounter;
            }
        }
    }
}
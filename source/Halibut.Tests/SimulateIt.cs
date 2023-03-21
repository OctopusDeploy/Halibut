using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class SimulateIt
    {
        
        [Test]
        public async Task Example()
        {
            var services = new DelegateServiceFactory();
            DoSomeActionService doSomeActionService = new DoSomeActionService();
            services.Register<IDoSomeActionService>(() => doSomeActionService);
            services.Register<IEchoService>(() => new EchoService());
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            {
                var octopusPort = octopus.Listen();
                var portForwarder = new PortForwarder(new Uri("https://localhost:" + octopusPort), TimeSpan.Zero);
                using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                {
                        
                    octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                    tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + portForwarder.PublicEndpoint.Port), Certificates.OctopusPublicThumbprint));

                    var svc = octopus.CreateClient<IDoSomeActionService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                    
                    var iEchoService = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                    iEchoService.SayHello("hello");

                    TestContext.WriteLine("Waiting");
                    Thread.Sleep(31000);
                    Stopwatch sw = Stopwatch.StartNew();
                    
                    
                    foreach (var portForwarderPump in portForwarder.Pumps)
                    {
                        portForwarderPump.Dispose();
                    }


                    for (int i = 0; i < 2; i++)
                    {
                        try
                        {
                            iEchoService.SayHello("hello");
                        }
                        catch (Exception)
                        {
                        }
                    }

                    sw.Stop();
                    Console.WriteLine(sw.Elapsed.TotalSeconds);
                    TestContext.WriteLine(sw.Elapsed.TotalSeconds);
                    
                    File.WriteAllText("/tmp/totalseconds", "" + sw.Elapsed.TotalSeconds);
                }
            }
        }
    }
}
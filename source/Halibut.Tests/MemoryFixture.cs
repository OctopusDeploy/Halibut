using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using NUnit.Framework;

namespace Halibut.Tests
{
    public interface ICalculatorService
    {
        long Add(long a, long b);
    }

    public class CalculatorService : ICalculatorService
    {
        public long Add(long a, long b)
        {
            //Console.WriteLine($"Got a request to add {a} + {b}");
            return a + b;
        }
    }
    
    [TestFixture]
    public class MemoryFixture
    {
        const int Clients = 5;
        const int RequestsPerClient = 5;

        [Test]
        [DotMemoryUnit(SavingStrategy = SavingStrategy.OnCheckFail, Directory = @"c:\temp\dotmemoryunit", WorkspaceNumberLimit = 5, DiskSpaceLimit = 104857600)]
        public void TcpClientsAreDisposedCorrectly()
        {
            if (!dotMemoryApi.IsEnabled)
                Assert.Inconclusive("This test is meant to be run under dotMemory Unit. In your IDE, right click on the test icon and choose 'Run under dotMemory Unit'.");

            using (RunServer(Certificates.Octopus, out var port))
            {
                for (var i = 0; i < Clients; i++)
                    RunListeningClient(Certificates.TentacleListening, port, Certificates.OctopusPublicThumbprint);
                for (var i = 0; i < Clients; i++)
                    RunPollingClient(Certificates.TentaclePolling, Certificates.TentaclePollingPublicThumbprint);
#if SUPPORTS_WEB_SOCKET_CLIENT
                for (var i = 0; i < Clients; i++)
                    RunWebSocketPollingClient(Certificates.TentaclePolling, Certificates.TentaclePollingPublicThumbprint, Certificates.OctopusPublicThumbprint);
#endif

                //https://dotnettools-support.jetbrains.com/hc/en-us/community/posts/360000088690-How-reproduce-DotMemory-s-Force-GC-button-s-behaviour-on-code-with-c-?page=1#community_comment_360000072750
                for (var i = 0; i < 4; i++)
                {
                    GC.Collect(2, GCCollectionMode.Forced, true);
                    GC.WaitForPendingFinalizers();
                }

                //server listen doesn't keep a port
                //server polling doesn't keep a port
                //server websocket polling _keeps_ a port
                //client listening _keeps_ a port
                //client polling doesn't keep a port
                //client websocket polling doesn't keep a port

                const int expectedTcpClientCount = 2;
                dotMemory.Check(memory => { Assert.That(memory.GetObjects(x => x.Type.Is<TcpClient>()).ObjectsCount, Is.LessThanOrEqualTo(expectedTcpClientCount)); });
            }
        }

        static HalibutRuntime RunServer(X509Certificate2 serverCertificate, out int port)
        {
            var services = new DelegateServiceFactory();
            services.Register<ICalculatorService>(() => new CalculatorService());

            var server = new HalibutRuntime(services, serverCertificate);
            
            //set up listening  
            server.Trust(Certificates.TentacleListeningPublicThumbprint);
            port = server.Listen();
            
            //setup polling
            var serviceEndPoint = new ServiceEndPoint(new Uri("https://localhost:8433"), Certificates.TentaclePollingPublicThumbprint);
            server.Poll(new Uri("poll://SQ-TENTAPOLL"), serviceEndPoint);
            
            //setup polling websocket
            AddSslCertToLocalStoreAndRegisterFor("0.0.0.0:8434");
            var endPoint = new ServiceEndPoint(new Uri("wss://localhost:8434/Halibut"), Certificates.SslThumbprint);
            server.Poll(new Uri("poll://SQ-WEBSOCKETPOLL"), endPoint);
            
            return server;
        }

        static void RunListeningClient(X509Certificate2 clientCertificate, int port, string remoteThumbprint, bool expectSuccess = true)
        {
            using (var runtime = new HalibutRuntime(clientCertificate))
            {
                var calculator = runtime.CreateClient<ICalculatorService>($"https://localhost:{port}/", remoteThumbprint);
                MakeRequest(calculator, "listening", expectSuccess);
            }
        }
        
        static void RunPollingClient(X509Certificate2 clientCertificate, string remoteThumbprint, bool expectSuccess = true)
        {
            using (var runtime = new HalibutRuntime(clientCertificate))
            {
                runtime.Listen(new IPEndPoint(IPAddress.IPv6Any, 8433));
                runtime.Trust(Certificates.OctopusPublicThumbprint);
                var endpoint = new ServiceEndPoint("poll://SQ-TENTAPOLL", remoteThumbprint);
                
                var calculator = runtime.CreateClient<ICalculatorService>(endpoint);
    
                MakeRequest(calculator, "polling", expectSuccess);
            }
        }
        
        static void RunWebSocketPollingClient(X509Certificate2 clientCertificate, string remoteThumbprint, string trustedCertificate, bool expectSuccess = true)
        {
            using (var runtime = new HalibutRuntime(clientCertificate))
            {
                runtime.ListenWebSocket("https://+:8434/Halibut");
                runtime.Trust(trustedCertificate);
                var endpoint = new ServiceEndPoint("poll://SQ-WEBSOCKETPOLL", remoteThumbprint);
                var calculator = runtime.CreateClient<ICalculatorService>(endpoint);
    
                MakeRequest(calculator, "websocket polling", expectSuccess);
            }
        }

        static void MakeRequest(ICalculatorService calculator, string requestType, bool expectSuccess)
        {
            for (var i = 0; i < RequestsPerClient; i++)
            {
                try
                {
                    var result = calculator.Add(12, 18);
                    Assert.That(result, Is.EqualTo(30));
                    if (!expectSuccess)
                        Assert.Fail(DateTime.Now.ToString("s") + ": Wasn't expecting this test to pass");
                }
                catch (Exception)
                {
                    if (expectSuccess)
                    {
                        throw;
                    }
                }
            }
        }

        static void AddSslCertToLocalStoreAndRegisterFor(string address)
        {
            var certificate = Certificates.Ssl;
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
            store.Close();

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("netsh", $"http add sslcert ipport={address} certhash={certificate.Thumbprint} appid={{2e282bfb-fce9-40fc-a594-2136043e1c8f}}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            proc.Start();
            proc.WaitForExit();
            var output = proc.StandardOutput.ReadToEnd();

            if (proc.ExitCode != 0 && !output.Contains("Cannot create a file when that file already exists"))
            {
                Console.WriteLine(output);
                Console.WriteLine(proc.StandardError.ReadToEnd());
                throw new Exception("Could not bind cert to port");
            }
        }
    }
}

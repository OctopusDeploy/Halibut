using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using FluentAssertions;
using Halibut.ServiceModel;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using NUnit.Framework;
using Serilog;

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
            return a + b;
        }
    }

    [TestFixture]
    public class MemoryFixture
    {
        const int NumberOfClients = 10;
        const int RequestsPerClient = 10;
        const int SecondsToGarbageCollect = 20;

        [Test]
        [DotMemoryUnit(SavingStrategy = SavingStrategy.OnCheckFail, Directory = @"c:\temp\dotmemoryunit", WorkspaceNumberLimit = 5, DiskSpaceLimit = 104857600)]
        public void TcpClientsAreDisposedCorrectly()
        {
            if (!dotMemoryApi.IsEnabled)
                Assert.Inconclusive("This test is meant to be run under dotMemory Unit. In your IDE, right click on the test icon and choose 'Run under dotMemory Unit'.");

            Log.Logger = new LoggerConfiguration()
                .WriteTo.NUnitOutput()
                .CreateLogger();

            using (var server = RunServer(Certificates.Octopus, out var port))
            {
                var expectedTcpClientCount = 1; //server listen = 1 tcpclient
                //valid requests
                for (var i = 0; i < NumberOfClients; i++)
                    RunListeningClient(Certificates.TentacleListening, port, Certificates.OctopusPublicThumbprint);
                for (var i = 0; i < NumberOfClients; i++)
                {
                    expectedTcpClientCount++; // each time the server polls, it keeps a tcpclient (as we dont have support to say StopPolling)
                    RunPollingClient(server, Certificates.TentaclePolling, Certificates.TentaclePollingPublicThumbprint);
                }

#if SUPPORTS_WEB_SOCKET_CLIENT
                for (var i = 0; i < NumberOfClients; i++)
                    RunWebSocketPollingClient(server, Certificates.TentaclePolling, Certificates.TentaclePollingPublicThumbprint, Certificates.OctopusPublicThumbprint);
#endif

                //https://dotnettools-support.jetbrains.com/hc/en-us/community/posts/360000088690-How-reproduce-DotMemory-s-Force-GC-button-s-behaviour-on-code-with-c-?page=1#community_comment_360000072750
                for (var i = 0; i < 4; i++)
                {
                    GC.Collect(2, GCCollectionMode.Forced, true);
                    GC.WaitForPendingFinalizers();
                }

                ShouldEventually(() =>
                {
                    var tcpClientCount = 0;
                    dotMemory.Check(memory => { tcpClientCount = memory.GetObjects(x => x.Type.Is<TcpClient>()).ObjectsCount; });
                    Console.WriteLine($"Found {tcpClientCount} instances of TcpClient still in memory.");
                    tcpClientCount.Should().BeLessOrEqualTo(expectedTcpClientCount, "Unexpected number of TcpClient objects in memory");
                }, TimeSpan.FromSeconds(SecondsToGarbageCollect));
            }
        }

        void ShouldEventually(Action test, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    test();
                    return;
                }
                catch (Exception)
                {
                    if (stopwatch.ElapsedMilliseconds >= timeout.TotalMilliseconds)
                    {
                        throw;
                    }
                    Thread.Sleep(100);
                    // No Timeout, lets try again
                }
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

            //setup polling websocket
            AddSslCertToLocalStoreAndRegisterFor("0.0.0.0:8434");

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

        static void RunPollingClient(HalibutRuntime server, X509Certificate2 clientCertificate, string remoteThumbprint, bool expectSuccess = true)
        {
            using (var runtime = new HalibutRuntime(clientCertificate))
            {
                runtime.Listen(new IPEndPoint(IPAddress.IPv6Any, 8433));
                runtime.Trust(Certificates.OctopusPublicThumbprint);

                //setup polling
                var serverEndpoint = new ServiceEndPoint(new Uri("https://localhost:8433"), Certificates.TentaclePollingPublicThumbprint)
                {
                    TcpClientConnectTimeout = TimeSpan.FromSeconds(5)
                };
                server.Poll(new Uri("poll://SQ-TENTAPOLL"), serverEndpoint);

                var clientEndpoint = new ServiceEndPoint("poll://SQ-TENTAPOLL", remoteThumbprint);

                var calculator = runtime.CreateClient<ICalculatorService>(clientEndpoint);

                MakeRequest(calculator, "polling", expectSuccess);

                runtime.Disconnect(clientEndpoint);
            }
        }

        static void RunWebSocketPollingClient(HalibutRuntime server, X509Certificate2 clientCertificate, string remoteThumbprint, string trustedCertificate, bool expectSuccess = true)
        {
            using (var runtime = new HalibutRuntime(clientCertificate))
            {
                runtime.ListenWebSocket("https://+:8434/Halibut");
                runtime.Trust(trustedCertificate);

                var serverEndpoint = new ServiceEndPoint(new Uri("wss://localhost:8434/Halibut"), Certificates.SslThumbprint)
                {
                    TcpClientConnectTimeout = TimeSpan.FromSeconds(5)
                };
                server.Poll(new Uri("poll://SQ-WEBSOCKETPOLL"), serverEndpoint);

                var clientEndpoint = new ServiceEndPoint("poll://SQ-WEBSOCKETPOLL", remoteThumbprint);
                var calculator = runtime.CreateClient<ICalculatorService>(clientEndpoint);

                MakeRequest(calculator, "websocket polling", expectSuccess);

                runtime.Disconnect(clientEndpoint);
            }
        }

        // ReSharper disable once UnusedParameter.Local
        static void MakeRequest(ICalculatorService calculator, string requestType, bool expectSuccess)
        {
            for (var i = 0; i < RequestsPerClient; i++)
                try
                {
                    var result = calculator.Add(12, 18);
                    Assert.That(result, Is.EqualTo(30));
                    if (!expectSuccess)
                        Assert.Fail(DateTime.Now.ToString("s") + ": Wasn't expecting this test to pass");
                }
                catch (Exception)
                {
                    if (expectSuccess) throw;
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
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    [NonParallelizable]
    public class WebSocketUsageFixture
    {
        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            return services;
        }

        [Test]
        [WindowsTestAttribute]
        public void HalibutSerializerIsKeptUpToDateWithWebSocketPollingTentacle()
        {
            var services = GetDelegateServiceFactory();
            services.Register<ISupportedServices>(() => new SupportedServices());
            const int octopusPort = 8450;
            AddSslCertToLocalStoreAndRegisterFor("0.0.0.0:" + octopusPort);

            try
            {
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                {
                    octopus.ListenWebSocket($"https://+:{octopusPort}/Halibut");
                    octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                    tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri($"wss://localhost:{octopusPort}/Halibut"), Certificates.SslThumbprint));

                    // This is here to exercise the path where the Listener's (web socket) handle loop has the protocol (with type serializer) built before the type is registered                     
                    var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                    // This must come before CreateClient<ISupportedServices> for the situation to occur
                    echo.SayHello("Deploy package A").Should().Be("Deploy package A" + "...");

                    var svc = octopus.CreateClient<ISupportedServices>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                    // This must happen before the message loop in MessageExchangeProtocol restarts (timeout, exception, or end) for the error to occur
                    svc.GetLocation(new MapLocation {Latitude = -27, Longitude = 153}).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
                }
            }
            catch (NotSupportedException nse) when (nse.Message == "The netstandard build of this library cannot act as the client in a WebSocket polling setup")
            {
                Assert.Inconclusive("This test cannot run on the netstandard build");
            }
            finally
            {
                RemoveSslCertBindingFor("0.0.0.0:" + octopusPort);
            }
        }

        [Test]
        [WindowsTestAttribute]
        public void OctopusCanSendMessagesToWebSocketPollingTentacle()
        {
            var services = GetDelegateServiceFactory();
            services.Register<ISupportedServices>(() => new SupportedServices());
            const int octopusPort = 8450;
            AddSslCertToLocalStoreAndRegisterFor("0.0.0.0:" + octopusPort);

            try
            {
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                {
                    octopus.ListenWebSocket($"https://+:{octopusPort}/Halibut");
                    octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                    tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri($"wss://localhost:{octopusPort}/Halibut"), Certificates.SslThumbprint));

                    var svc = octopus.CreateClient<ISupportedServices>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                    for (var i = 1; i < 100; i++)
                    {
                        var i1 = i;
                        svc.GetLocation(new MapLocation {Latitude = -i, Longitude = i}).Should().Match<MapLocation>(x => x.Latitude == i1 && x.Longitude == -i1);
                    }
                }
            }
            catch (NotSupportedException nse) when (nse.Message == "The netstandard build of this library cannot act as the client in a WebSocket polling setup")
            {
                Assert.Inconclusive("This test cannot run on the netstandard build");
            }
            finally
            {
                RemoveSslCertBindingFor("0.0.0.0:" + octopusPort);
            }
        }

        static void AddSslCertToLocalStoreAndRegisterFor(string address)
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(Certificates.Ssl);
            store.Close();

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("netsh", $"http add sslcert ipport={address} certhash={Certificates.SslThumbprint} appid={{2e282bfb-fce9-40fc-a594-2136043e1c8f}}")
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

        static void RemoveSslCertBindingFor(string address)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("netsh", $"http delete sslcert ipport={address}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            proc.Start();
            proc.WaitForExit();
            var output = proc.StandardOutput.ReadToEnd();

            if (proc.ExitCode != 0)
            {
                Console.WriteLine(output);
                Console.WriteLine(proc.StandardError.ReadToEnd());
                throw new Exception("The system cannot find the file specified");
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class UsageFixture
    {
        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            return services;
        }

        [Test]
        public void OctopusCanDiscoverTentacle()
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();

                var info = octopus.Discover(new Uri("https://localhost:" + tentaclePort));
                info.RemoteThumbprint.Should().Be(Certificates.TentacleListeningPublicThumbprint);
            }
        }
       

        [Test]
        public void OctopusCanSendMessagesToListeningTentacle()
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < 2000; i++)
                {
                    echo.SayHello("Deploy package A").Should().Be("Deploy package A...");
                }

                Console.WriteLine("Complete in {0:n0}ms", watch.ElapsedMilliseconds);
            }
        }

        [Test]
        public void OctopusCanSendMessagesToPollingTentacle()
        {
            var services = GetDelegateServiceFactory();
            services.Register<ISupportedServices>(() => new SupportedServices());
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var svc = octopus.CreateClient<ISupportedServices>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                for (var i = 1; i < 100; i++)
                {
                    var i1 = i;
                    svc.GetLocation(new MapLocation { Latitude = -i, Longitude = i }).Should().Match<MapLocation>(x => x.Latitude == i1 && x.Longitude == -i1);
                }
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
                        svc.GetLocation(new MapLocation { Latitude = -i, Longitude = i }).Should().Match<MapLocation>(x => x.Latitude == i1 && x.Longitude == -i1);
                    }
                }
            }
            catch(NotSupportedException nse) when (nse.Message == "The netstandard build of this library cannot act as the client in a WebSocket polling setup")
            {
                Assert.Inconclusive("This test cannot run on the netstandard build");
            }
            finally
            {
                RemoveSslCertBindingFor("0.0.0.0:" + octopusPort);
            }
        }

        [Test]
        public void HalibutSerializerIsKeptUpToDateWithPollingTentacle()
        {
            var services = GetDelegateServiceFactory();
            services.Register<ISupportedServices>(() => new SupportedServices());
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                // This is here to exercise the path where the Listener's (web socket) handle loop has the protocol (with type serializer) built before the type is registered                     
                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                // This must come before CreateClient<ISupportedServices> for the situation to occur
                echo.SayHello("Deploy package A").Should().Be("Deploy package A" + "..."); 

                var svc = octopus.CreateClient<ISupportedServices>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                // This must happen before the message loop in MessageExchangeProtocol restarts (timeout, exception, or end) for the error to occur
                svc.GetLocation(new MapLocation { Latitude = -27, Longitude = 153 }).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
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
                    svc.GetLocation(new MapLocation { Latitude = -27, Longitude = 153 }).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
                }
            }
            catch(NotSupportedException nse) when (nse.Message == "The netstandard build of this library cannot act as the client in a WebSocket polling setup")
            {
                Assert.Inconclusive("This test cannot run on the netstandard build");
            }
            finally
            {
                RemoveSslCertBindingFor("0.0.0.0:" + octopusPort);
            }
        }
        
        [Test]
        public void StreamsCanBeSentToListening()
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                for (var i = 0; i < 100; i++)
                {
                    var count = echo.CountBytes(DataStream.FromBytes(data));
                    count.Should().Be(1024 * 1024 + 15);
                }
            }
        }

        [Test]
        public void StreamsCanBeSentToPolling()
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                for (var i = 0; i < 100; i++)
                {
                    var count = echo.CountBytes(DataStream.FromBytes(data));
                    count.Should().Be(1024 * 1024 + 15);
                }
            }
        }
        
        
        [Test]
        public void SmallStreamsCanBeSentToPolling()
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                var data = "I have gazed into the Omniscience, and it has gazed into me.".ToUtf8();
                

                for (var i = 0; i < 3; i++)
                {
                    var count = echo.CountBytes(DataStream.FromBytes(data));
                    count.Should().Be(data.Length);
                }
            }
        }
        
        
        [Test]
        public void StreamsCanBeSentToPollingWithLatency()
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                using (var loadBalancer = new PortForwarder(new Uri("https://localhost:" + octopusPort), TimeSpan.FromMilliseconds(10)))
                {

                    tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + loadBalancer.PublicEndpoint.Port), Certificates.OctopusPublicThumbprint));

                    var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                    var data = new byte[1024 * 1024 + 15];
                    new Random().NextBytes(data);

                    for (var i = 0; i < 100; i++)
                    {
                        var count = echo.CountBytes(DataStream.FromBytes(data));
                        count.Should().Be(1024 * 1024 + 15);
                    }
                }
            }
        }

        [Test]
        public void SupportsDifferentServiceContractMethods()
        {
            var services = GetDelegateServiceFactory();
            services.Register<ISupportedServices>(() => new SupportedServices());
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);
                var tentaclePort = tentacleListening.Listen();

                var echo = octopus.CreateClient<ISupportedServices>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                echo.MethodReturningVoid(12, 14);

                echo.Hello().Should().Be("Hello");
                echo.Hello("a").Should().Be("Hello a");
                echo.Hello("a", "b").Should().Be("Hello a b");
                echo.Hello("a", "b", "c").Should().Be("Hello a b c");
                echo.Hello("a", "b", "c", "d").Should().Be("Hello a b c d");
                echo.Hello("a", "b", "c", "d", "e").Should().Be("Hello a b c d e");
                echo.Hello("a", "b", "c", "d", "e", "f").Should().Be("Hello a b c d e f");
                echo.Hello("a", "b", "c", "d", "e", "f", "g").Should().Be("Hello a b c d e f g");
                echo.Hello("a", "b", "c", "d", "e", "f", "g", "h").Should().Be("Hello a b c d e f g h");
                echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i").Should().Be("Hello a b c d e f g h i");
                echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j").Should().Be("Hello a b c d e f g h i j");
                echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k").Should().Be("Hello a b c d e f g h i j k");

                echo.Add(1, 2).Should().Be(3);
                echo.Add(1.00, 2.00).Should().Be(3.00);
                echo.Add(1.10M, 2.10M).Should().Be(3.20M);

                echo.Ambiguous("a", "b").Should().Be("Hello string");
                echo.Ambiguous("a", new Tuple<string, string>("a", "b")).Should().Be("Hello tuple");

                var ex = Assert.Throws<HalibutClientException>(() => echo.Ambiguous("a", (string)null));
                ex.Message.Should().Contain("Ambiguous");

                echo.GetLocation(new MapLocation { Latitude = -27, Longitude = 153 }).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
        }

        [Test]
        public void StreamsCanBeSentToListeningWithProgressReporting()
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var progressReported = new List<int>();

                var data = new byte[1024 * 1024 * 16 + 15];
                new Random().NextBytes(data);
                var stream = new MemoryStream(data);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                var count = echo.CountBytes(DataStream.FromStream(stream, progressReported.Add));
                count.Should().Be(1024 * 1024 * 16 + 15);

                progressReported.Should().ContainInOrder(Enumerable.Range(1, 100));
            }
        }

        [TestCase("https://127.0.0.1:{port}")]
        [TestCase("https://127.0.0.1:{port}/")]
        [TestCase("https://localhost:{port}")]
        [TestCase("https://localhost:{port}/")]
        [TestCase("https://{machine}:{port}")]
        [TestCase("https://{machine}:{port}/")]
        public async Task SupportsHttpsGet(string uriFormat)
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var listenPort = octopus.Listen();
                var uri = uriFormat.Replace("{machine}", Dns.GetHostName()).Replace("{port}", listenPort.ToString());

                var result = await DownloadStringIgnoringCertificateValidation(uri);

                result.Should().Be("<html><body><p>Hello!</p></body></html>");
            }
        }

        [TestCase("<html><body><h1>Welcome to Octopus Server!</h1><p>It looks like everything is running just like you expected, well done.</p></body></html>", null)]
        [TestCase("Simple text works too!", null)]
        [TestCase("", null)]
        [TestCase(null, "<html><body><p>Hello!</p></body></html>")]
        public async Task CanSetCustomFriendlyHtmlPage(string html, string expectedResult = null)
        {
            var services = GetDelegateServiceFactory();
            expectedResult = expectedResult ?? html; // Handle the null case which reverts to default html

            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                octopus.SetFriendlyHtmlPageContent(html);
                var listenPort = octopus.Listen();

                var result = await DownloadStringIgnoringCertificateValidation("https://localhost:" + listenPort);

                result.Should().Be(expectedResult);
            }
        }

        [Test]
        public async Task CanSetCustomFriendlyHtmlPageHeaders()
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                octopus.SetFriendlyHtmlPageHeaders(new Dictionary<string, string> {{"X-Content-Type-Options", "nosniff"}, {"X-Frame-Options", "DENY"}});
                var listenPort = octopus.Listen();

                var result = await GetHeadersIgnoringCertificateValidation("https://localhost:" + listenPort);

                result.Should().Contain(x => x.Key == "X-Content-Type-Options" && x.Value == "nosniff");
                result.Should().Contain(x => x.Key == "X-Frame-Options" && x.Value == "DENY");
            }
        }

        [Test]
        [System.ComponentModel.Description("Connecting over a non-secure connection should cause the socket to be closed by the server. The socket used to be held open indefinitely for any failure to establish an SslStream.")]
        public void ConnectingOverHttpShouldFailQuickly()
        {
            var task = Task.Run(() => DoConnectingOverHttpShouldFailQuickly());
            if (!task.Wait(5000))
            {
                Assert.Fail("Test did not complete within timeout");
            }
        }

        void DoConnectingOverHttpShouldFailQuickly()
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var listenPort = octopus.Listen();
                Assert.ThrowsAsync<HttpRequestException>(() => DownloadStringIgnoringCertificateValidation("http://localhost:" + listenPort));
            }
        }

        static async Task<string> DownloadStringIgnoringCertificateValidation(string uri)
        {
            using (var httpClientHandler = new HttpClientHandler())
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                using (var client = new HttpClient(httpClientHandler))
                {
                    return await client.GetStringAsync(uri);
                }
            }
        }

        static async Task<List<KeyValuePair<string, string>>> GetHeadersIgnoringCertificateValidation(string uri)
        {
            using (var httpClientHandler = new HttpClientHandler())
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                using (var client = new HttpClient(httpClientHandler))
                {
                    var headers = new List<KeyValuePair<string, string>>();
                    var existingServerCertificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;
                    try
                    {
                        // We need to ignore server certificate validation errors - the server certificate is self-signed
                        ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
                        var response = await client.GetAsync(uri);
                        foreach (var key in response.Headers)
                        {
                            headers.Add(new KeyValuePair<string, string>(key.Key, key.Value.First()));
                        }
                    }
                    finally
                    {
                        // And restore it back to default behaviour
                        ServicePointManager.ServerCertificateValidationCallback = existingServerCertificateValidationCallback;
                    }

                    return headers;
                }
            }
        }

        static void AddSslCertToLocalStoreAndRegisterFor(string address)
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(Certificates.Ssl);
            store.Close();


            var proc = new Process()
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
            var proc = new Process()
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
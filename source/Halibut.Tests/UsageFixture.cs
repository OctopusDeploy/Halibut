using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
#if !NET40
using System.Net.Http;
#endif
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Newtonsoft.Json.Bson;
using Xunit;

namespace Halibut.Tests
{
    public class UsageFixture
    {
        DelegateServiceFactory services;

        public UsageFixture()
        {
            services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
        }

        [Fact]
        public async Task OctopusCanDiscoverTentacle()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();

                var info = await octopus.Discover(new Uri("https://localhost:" + tentaclePort)).ConfigureAwait(false);
                info.RemoteThumbprint.Should().Be(Certificates.TentacleListeningPublicThumbprint);

                await octopus.Stop().ConfigureAwait(false);
                await tentacleListening.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task OctopusCanSendMessagesToListeningTentacle()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                var greeting = await echo.SayHello("Deploy package A").ConfigureAwait(false);

                greeting.Should().Be("Deploy package A...");
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < 2000; i++)
                {
                    greeting = await echo.SayHello("Deploy package A").ConfigureAwait(false);
                    greeting.Should().Be("Deploy package A...");
                }

                Console.WriteLine("Complete in {0:n0}ms", watch.ElapsedMilliseconds);

                await octopus.Stop().ConfigureAwait(false);
                await tentacleListening.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task OctopusCanSendMessagesToPollingTentacle()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                for (var i = 0; i < 2000; i++)
                {
                    var greeting = await echo.SayHello("Deploy package A" + i).ConfigureAwait(false);

                    greeting.Should().Be("Deploy package A" + i + "...");
                }

                await octopus.Stop().ConfigureAwait(false);
                await tentaclePolling.Stop().ConfigureAwait(false);
            }
        }

#if HAS_SERVICE_POINT_MANAGER
        [Fact]
        public async Task OctopusCanSendMessagesToWebSocketPollingTentacle()
        {
            const int octopusPort = 8450;
            AddSslCertToLocalStoreAndRegisterFor("0.0.0.0:" + octopusPort);

            try
            {
                using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
                using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                {
                    octopus.ListenWebSocket($"https://+:{octopusPort}/Halibut");
                    octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                    tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri($"wss://localhost:{octopusPort}/Halibut"), Certificates.SslThumbprint));

                    var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                    for (var i = 0; i < 2000; i++)
                    {
                        var greeting = await echo.SayHello("Deploy package A" + i).ConfigureAwait(false);
                        greeting.Should().Be("Deploy package A" + i + "...");
                    }

                    await octopus.Stop().ConfigureAwait(false);
                    await tentaclePolling.Stop().ConfigureAwait(false);
                }
            }
            finally
            {
                RemoveSslCertBindingFor("0.0.0.0:" + octopusPort);
            }
        }
#endif

        [Fact]
        public async Task StreamsCanBeSentToListening()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                for (var i = 0; i < 100; i++)
                {
                    var count = await echo.CountBytes(DataStream.FromBytes(data)).ConfigureAwait(false);
                    count.Should().Be(1024 * 1024 + 15);
                }

                await octopus.Stop().ConfigureAwait(false);
                await tentacleListening.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task StreamsCanBeSentToPolling()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
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
                    var count = await echo.CountBytes(DataStream.FromBytes(data)).ConfigureAwait(false);
                    count.Should().Be(1024 * 1024 + 15);
                }

                await octopus.Stop().ConfigureAwait(false);
                await tentaclePolling.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task SupportsDifferentServiceContractMethods()
        {
            services.Register<ISupportedServices>(() => new SupportedServices());
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);
                var tentaclePort = tentacleListening.Listen();

                var echo = octopus.CreateClient<ISupportedServices>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                await echo.MethodReturningVoid(12, 14).ConfigureAwait(false);

                Assert.Equal("Hello", await echo.Hello().ConfigureAwait(false));
                Assert.Equal("Hello a", await echo.Hello("a").ConfigureAwait(false));
                Assert.Equal("Hello a b", await echo.Hello("a", "b").ConfigureAwait(false));
                Assert.Equal("Hello a b c", await echo.Hello("a", "b", "c").ConfigureAwait(false));
                Assert.Equal("Hello a b c d", await echo.Hello("a", "b", "c", "d").ConfigureAwait(false));
                Assert.Equal("Hello a b c d e", await echo.Hello("a", "b", "c", "d", "e").ConfigureAwait(false));
                Assert.Equal("Hello a b c d e f", await echo.Hello("a", "b", "c", "d", "e", "f").ConfigureAwait(false));
                Assert.Equal("Hello a b c d e f g", await echo.Hello("a", "b", "c", "d", "e", "f", "g").ConfigureAwait(false));
                Assert.Equal("Hello a b c d e f g h", await echo.Hello("a", "b", "c", "d", "e", "f", "g", "h").ConfigureAwait(false));
                Assert.Equal("Hello a b c d e f g h i", await echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i").ConfigureAwait(false));
                Assert.Equal("Hello a b c d e f g h i j", await echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j").ConfigureAwait(false));
                Assert.Equal("Hello a b c d e f g h i j k", await echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k").ConfigureAwait(false));

                Assert.Equal(3, await echo.Add(1, 2).ConfigureAwait(false));
                Assert.Equal(3.00, await echo.Add(1.00, 2.00).ConfigureAwait(false));
                Assert.Equal(3.20M, await echo.Add(1.10M, 2.10M).ConfigureAwait(false));

                Assert.Equal("Hello string", await echo.Ambiguous("a", "b").ConfigureAwait(false));
                Assert.Equal("Hello tuple", await echo.Ambiguous("a", new Tuple<string, string>("a", "b")).ConfigureAwait(false));

                var ex = await Assert.ThrowsAsync<HalibutClientException>(() => echo.Ambiguous("a", (string)null)).ConfigureAwait(false);
                ex.Message.Should().Contain("Ambiguous");

                await octopus.Stop().ConfigureAwait(false);
                await tentacleListening.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task StreamsCanBeSentToListeningWithProgressReporting()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var progressReported = new List<int>();

                var data = new byte[1024 * 1024 * 16 + 15];
                new Random().NextBytes(data);
                var stream = new MemoryStream(data);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                var count = await echo.CountBytes(DataStream.FromStream(stream, progressReported.Add)).ConfigureAwait(false);
                count.Should().Be(1024 * 1024 * 16 + 15);

                progressReported.Should().ContainInOrder(Enumerable.Range(1, 100));

                await octopus.Stop().ConfigureAwait(false);
                await tentacleListening.Stop().ConfigureAwait(false);
            }
        }

        [Theory]
        [InlineData("https://127.0.0.1:{port}")]
        [InlineData("https://127.0.0.1:{port}/")]
        [InlineData("https://localhost:{port}")]
        [InlineData("https://localhost:{port}/")]
        [InlineData("https://{machine}:{port}")]
        [InlineData("https://{machine}:{port}/")]
        public async Task SupportsHttpsGet(string uriFormat)
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var listenPort = octopus.Listen();
                var uri = uriFormat.Replace("{machine}", Environment.MachineName).Replace("{port}", listenPort.ToString());

                var result = DownloadStringIgnoringCertificateValidation(uri);

                result.Should().Be("<html><body><p>Hello!</p></body></html>");

                await octopus.Stop().ConfigureAwait(false);
            }
        }

        [Theory]
        [InlineData("<html><body><h1>Welcome to Octopus Server!</h1><p>It looks like everything is running just like you expected, well done.</p></body></html>", null)]
        [InlineData("Simple text works too!", null)]
        [InlineData("", null)]
        [InlineData(null, "<html><body><p>Hello!</p></body></html>")]
        public async Task CanSetCustomFriendlyHtmlPage(string html, string expectedResult = null)
        {
            expectedResult = expectedResult ?? html; // Handle the null case which reverts to default html

            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                octopus.SetFriendlyHtmlPageContent(html);
                var listenPort = octopus.Listen();

                var result = DownloadStringIgnoringCertificateValidation("https://localhost:" + listenPort);

                result.Should().Be(expectedResult);

                await octopus.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        [Description("Connecting over a non-secure connection should cause the socket to be closed by the server. The socket used to be held open indefinitely for any failure to establish an SslStream.")]
        public async Task ConnectingOverHttpShouldFailQuickly()
        {
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            
            var finishedTask = await Task.WhenAny(DoConnectingOverHttpShouldFailQuickly(), timeoutTask).ConfigureAwait(false);

            if (finishedTask.Equals(timeoutTask))
            {
                Assert.True(false, "Test did not complete within timeout");
            }
        }

        async Task DoConnectingOverHttpShouldFailQuickly()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var listenPort = octopus.Listen();
#if NET40
                Assert.Throws<WebException>(() => DownloadStringIgnoringCertificateValidation("http://localhost:" + listenPort));
#else
                Assert.Throws<HttpRequestException>(() => DownloadStringIgnoringCertificateValidation("http://localhost:" + listenPort));
#endif

                await octopus.Stop().ConfigureAwait(false);
            }
        }

        static string DownloadStringIgnoringCertificateValidation(string uri)
        {
#if NET40
            using (var webClient = new WebClient())
            {
                try
                {
                    // We need to ignore server certificate validation errors - the server certificate is self-signed
                    ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
                    return webClient.DownloadString(uri);
                }
                finally
                {
                    // And restore it back to default behaviour
                    ServicePointManager.ServerCertificateValidationCallback = null;
                }
            }
#else
            var handler = new HttpClientHandler();
            // We need to ignore server certificate validation errors - the server certificate is self-signed
            handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, errors) => true;
            using (var webClient = new HttpClient(handler))
            {
                return webClient.GetStringAsync(uri).GetAwaiter().GetResult();
            }
#endif
        }

#if HAS_SERVICE_POINT_MANAGER
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
#endif
    }
}
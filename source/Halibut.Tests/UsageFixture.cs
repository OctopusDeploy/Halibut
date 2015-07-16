using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
    public class UsageFixture
    {
        DelegateServiceFactory services;

        [SetUp]
        public void SetUp()
        {
            services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
        }

        [Test]
        public void OctopusCanDiscoverTentacle()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();

                var info = octopus.Discover(new Uri("https://localhost:" + tentaclePort));
                Assert.That(info.RemoteThumbprints, Is.EqualTo(Certificates.TentacleListeningPublicThumbprint));
            }
        }

        [Test]
        public void OctopusCanSendMessagesToListeningTentacle()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                Assert.That(echo.SayHello("Deploy package A"), Is.EqualTo("Deploy package A..."));
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < 2000; i++)
                {
                    Assert.That(echo.SayHello("Deploy package A"), Is.EqualTo("Deploy package A..."));
                }

                Console.WriteLine("Complete in {0:n0}ms", watch.ElapsedMilliseconds);
            }
        }

        [Test]
        public void OctopusCanSendMessagesToPollingTentacle()
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
                    Assert.That(echo.SayHello("Deploy package A" + i), Is.EqualTo("Deploy package A" + i + "..."));
                }
            }
        }

        [Test]
        public void StreamsCanBeSentToListening()
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
                    var count = echo.CountBytes(DataStream.FromBytes(data));
                    Assert.That(count, Is.EqualTo(1024 * 1024 + 15));
                }
            }
        }

        [Test]
        public void StreamsCanBeSentToPolling()
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
                    var count = echo.CountBytes(DataStream.FromBytes(data));
                    Assert.That(count, Is.EqualTo(1024 * 1024 + 15));
                }
            }
        }

        [Test]
        public void SupportsDifferentServiceContractMethods()
        {
            services.Register<ISupportedServices>(() => new SupportedServices());
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);
                var tentaclePort = tentacleListening.Listen();

                var echo = octopus.CreateClient<ISupportedServices>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                echo.MethodReturningVoid(12, 14);

                Assert.That(echo.Hello(), Is.EqualTo("Hello"));
                Assert.That(echo.Hello("a"), Is.EqualTo("Hello a"));
                Assert.That(echo.Hello("a", "b"), Is.EqualTo("Hello a b"));
                Assert.That(echo.Hello("a", "b", "c"), Is.EqualTo("Hello a b c"));
                Assert.That(echo.Hello("a", "b", "c", "d"), Is.EqualTo("Hello a b c d"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e"), Is.EqualTo("Hello a b c d e"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f"), Is.EqualTo("Hello a b c d e f"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g"), Is.EqualTo("Hello a b c d e f g"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h"), Is.EqualTo("Hello a b c d e f g h"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i"), Is.EqualTo("Hello a b c d e f g h i"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j"), Is.EqualTo("Hello a b c d e f g h i j"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k"), Is.EqualTo("Hello a b c d e f g h i j k"));

                Assert.That(echo.Add(1, 2), Is.EqualTo(3));
                Assert.That(echo.Add(1.00, 2.00), Is.EqualTo(3.00));
                Assert.That(echo.Add(1.10M, 2.10M), Is.EqualTo(3.20M));

                Assert.That(echo.Ambiguous("a", "b"), Is.EqualTo("Hello string"));
                Assert.That(echo.Ambiguous("a", new Tuple<string, string>("a", "b")), Is.EqualTo("Hello tuple"));

                var ex = Assert.Throws<HalibutClientException>(() => echo.Ambiguous("a", (string)null));
                Assert.That(ex.Message, Is.StringContaining("Ambiguous"));
            }
        }

        [Test]
        public void StreamsCanBeSentToListeningWithProgressReporting()
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

                var count = echo.CountBytes(DataStream.FromStream(stream, progressReported.Add));
                Assert.That(count, Is.EqualTo(1024 * 1024 * 16 + 15));

                CollectionAssert.AreEqual(Enumerable.Range(1, 100).ToList(), progressReported);
            }
        }

        [Test]
        [TestCase("https://127.0.0.1:{port}")]
        [TestCase("https://127.0.0.1:{port}/")]
        [TestCase("https://localhost:{port}")]
        [TestCase("https://localhost:{port}/")]
        [TestCase("https://{machine}:{port}")]
        [TestCase("https://{machine}:{port}/")]
        public void SupportsHttpsGet(string uriFormat)
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var listenPort = octopus.Listen();
                var uri = uriFormat.Replace("{machine}", Environment.MachineName).Replace("{port}", listenPort.ToString());
                
                var result = DownloadStringIgnoringCertificateValidation(uri);

                Assert.That(result, Is.EqualTo("<html><body><p>Hello!</p></body></html>"));
            }
        }

        [Test]
        [TestCase("<html><body><h1>Welcome to Octopus Server!</h1><p>It looks like everything is running just like you expected, well done.</p></body></html>", null)]
        [TestCase("Simple text works too!", null)]
        [TestCase("", null)]
        [TestCase(null, "<html><body><p>Hello!</p></body></html>")]
        public void CanSetCustomFriendlyHtmlPage(string html, string expectedResult = null)
        {
            expectedResult = expectedResult ?? html; // Handle the null case which reverts to default html

            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                octopus.SetFriendlyHtmlPageContent(html);
                var listenPort = octopus.Listen();

                var result = DownloadStringIgnoringCertificateValidation("https://localhost:" + listenPort);

                Assert.That(result, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        [Timeout(5000)]
        [Description("Connecting over a non-secure connection should cause the socket to be closed by the server. The socket used to be held open indefinitely for any failure to establish an SslStream.")]
        public void ConnectingOverHttpShouldFailQuickly()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var listenPort = octopus.Listen();
                Assert.Throws<WebException>(() => DownloadStringIgnoringCertificateValidation("http://localhost:" + listenPort));
            }
        }

        static string DownloadStringIgnoringCertificateValidation(string uri)
        {
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
        }
    }
}
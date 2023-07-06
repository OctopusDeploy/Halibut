using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class UsageFixture
    {
        [Test]
        public void OctopusCanDiscoverTentacle()
        {
            using (var clientAndService = ClientServiceBuilder
                       .Listening()
                       .WithEchoService()
                       .Build())
            {
                var info = clientAndService.Octopus.Discover(clientAndService.ServiceUri);
                info.RemoteThumbprint.Should().Be(Certificates.TentacleListeningPublicThumbprint);
            }
        }


        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public void OctopusCanSendMessagesToTentacle_WithEchoService(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");

                for (var i = 0; i < 2000; i++)
                {
                    echo.SayHello($"Deploy package A {i}").Should().Be($"Deploy package A {i}...");
                }
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public void OctopusCanSendMessagesToTentacle_WithSupportedServices(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .WithSupportedServices()
                       .Build())
            {
                var svc = clientAndService.CreateClient<ISupportedServices>();
                for (var i = 1; i < 100; i++)
                {
                    {
                        var i1 = i;
                        svc.GetLocation(new MapLocation { Latitude = -i, Longitude = i }).Should().Match<MapLocation>(x => x.Latitude == i1 && x.Longitude == -i1);
                    }
                }
            }
        }

        [Test]
        public void HalibutSerializerIsKeptUpToDateWithPollingTentacle()
        {
            using (var clientAndService = ClientServiceBuilder
                       .Polling()
                       .WithEchoService()
                       .WithSupportedServices()
                       .Build()){

                // This is here to exercise the path where the Listener's (web socket) handle loop has the protocol (with type serializer) built before the type is registered
                var echo = clientAndService.CreateClient<IEchoService>();
                // This must come before CreateClient<ISupportedServices> for the situation to occur
                echo.SayHello("Deploy package A").Should().Be("Deploy package A" + "...");

                var svc = clientAndService.CreateClient<ISupportedServices>();
                // This must happen before the message loop in MessageExchangeProtocol restarts (timeout, exception, or end) for the error to occur
                svc.GetLocation(new MapLocation { Latitude = -27, Longitude = 153 }).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public void StreamsCanBeSent(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();

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
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public void StreamsCanBeSentWithLatency(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .WithPortForwarding(octopusPort => PortForwarderUtil.ForwardingToLocalPort(octopusPort).WithSendDelay(TimeSpan.FromMilliseconds(20)).Build())
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();

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
        [TestCase(ServiceConnectionType.Polling, 1)]
        [TestCase(ServiceConnectionType.Listening, 2)]
        [TestCase(ServiceConnectionType.Polling, 2)]
        [TestCase(ServiceConnectionType.Listening, 3)]
        [TestCase(ServiceConnectionType.Polling, 3)]
        public void StreamsCanBeSentWithLatencyAndTheLastNBytesArriveLate(ServiceConnectionType serviceConnectionType, int numberOfBytesToDelaySending)
        {
            using (var clientAndService = ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .WithPortForwarding(octopusPort => PortForwarderUtil.ForwardingToLocalPort(octopusPort)
                           .WithSendDelay(TimeSpan.FromMilliseconds(20))
                           .WithNumberOfBytesToDelaySending(numberOfBytesToDelaySending)
                           .Build())
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();

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
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public void SupportsDifferentServiceContractMethods(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = ClientServiceBuilder
                     .ForMode(serviceConnectionType)
                     .WithEchoService()
                     .WithSupportedServices()
                     .Build())
            {
                var echo = clientAndService.CreateClient<ISupportedServices>();
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

                var ex = Assert.Throws<AmbiguousMethodMatchHalibutClientException>(() => echo.Ambiguous("a", (string)null));
                ex.Message.Should().Contain("Ambiguous");

                echo.GetLocation(new MapLocation { Latitude = -27, Longitude = 153 }).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public void StreamsCanBeSentWithProgressReporting(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .Build())
            {
                var progressReported = new List<int>();

                var data = new byte[1024 * 1024 * 16 + 15];
                new Random().NextBytes(data);
                var stream = new MemoryStream(data);

                var echo = clientAndService.CreateClient<IEchoService>();

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
        public async Task ConnectingOverHttpShouldFailQuickly()
        {
            var logger = new SerilogLoggerBuilder().Build();
            using (var octopus = new HalibutRuntime(GetDelegateServiceFactory(), Certificates.Octopus))
            {
                logger.Information("Halibut runtime created.");
                var listenPort = octopus.Listen();
                logger.Information("Got port to listen on..");
                var sw = new Stopwatch();
                Assert.ThrowsAsync<HttpRequestException>(() =>
                {
                    logger.Information("Sending request.");
                    sw.Start();
                    try
                    {
                        return DownloadStringIgnoringCertificateValidation("http://localhost:" + listenPort);
                    }
                    finally
                    {
                        sw.Stop();
                    }
                });


                sw.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(5));
            }
        }

        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            return services;
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


    }
}
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class FailureModesFixture
    {
        static DelegateServiceFactory GetStubDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            return services;
        }

        [Test]
        public void FailsWhenSendingToPollingMachineButNothingPicksItUp()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var endpoint = new ServiceEndPoint("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint)
                {
                    TcpClientConnectTimeout = TimeSpan.FromSeconds(1),
                    PollingRequestQueueTimeout = TimeSpan.FromSeconds(5)
                };
                var echo = octopus.CreateClient<IEchoService>(endpoint);
                var error = Assert.Throws<HalibutClientException>(() => echo.SayHello("Paul"));
                error.Message.Should().Contain("the polling endpoint did not collect the request within the allowed time");
            }
        }

        [Test]
        public void FailWhenServerThrowsAnException()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                ex.Message.Should().Contain("at Halibut.Tests.TestServices.EchoService.Crash()").And.Contain("divide by zero");
            }
        }

        [Test]
        public void FailWhenServerThrowsAnExceptionOnPolling()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                ex.Message.Should().Contain("at Halibut.Tests.TestServices.EchoService.Crash()").And.Contain("divide by zero");
            }
        }

#if SUPPORTS_WEB_SOCKET_CLIENT
        [Test]
        public void FailWhenServerThrowsAnExceptionOnWebSocketPolling()
        {
            var services = GetStubDelegateServiceFactory();

            using (var prereq = new WebSocketListeningPrerequisites())
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                octopus.ListenWebSocket($"https://+:{prereq.Port}");
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri($"wss://localhost:{prereq.Port}"), Certificates.SslThumbprint));

                var echo = octopus.CreateClient<IEchoService>(new ServiceEndPoint("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint)
                {
                    PollingRequestQueueTimeout = TimeSpan.FromSeconds(5)
                });

                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                ex.Message.Should().Contain("at Halibut.Tests.TestServices.EchoService.Crash()").And.Contain("divide by zero");
            }
        }
#endif

        [Test]
        public void FailOnInvalidHostname()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000", Certificates.TentacleListeningPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                var message = ex.Message;

                message.Should().Contain("when sending a request to 'https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000/', before the request");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    message.Should().Contain("No such host is known");
                }
                else
                {
                    // Failed with: An error occurred when sending a request to 'https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000/', before the request could begin: Name or service not known, but found False.
                    new[] { "No such device or address", "Resource temporarily unavailable", "Name or service not known" }.Any(message.Contains).Should().BeTrue($"Message does not match known strings: {message}");
                }
            }
        }

        [Test]
        public void FailOnInvalidPort()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var endpoint = new ServiceEndPoint("https://google.com:88", Certificates.TentacleListeningPublicThumbprint)
                {
                    TcpClientConnectTimeout = TimeSpan.FromSeconds(2),
                    RetryCountLimit = 2
                };
                var echo = octopus.CreateClient<IEchoService>(endpoint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                ex.Message.Should().Be("An error occurred when sending a request to 'https://google.com:88/', before the request could begin: The client was unable to establish the initial connection within the timeout 00:00:02.");
            }
        }

        [Test]
        public void FailWhenListeningAndClientPresentsWrongCertificate()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust("EXPECTED_CLIENT_THUMBPRINT");

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                Action action = () => echo.SayHello("World");

                // Various platforms throw different messages:
                // - Unable to receive the remote identity; the identity line was empty
                // - An existing connection was forcibly closed by the remote host
                // - An established connection was aborted by the software in your host machine
                action.Should().Throw<HalibutClientException>();
            }
        }

        [Test]
        public void FailWhenListeningAndServerPresentsWrongCertificate()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>(new ServiceEndPoint("https://localhost:" + tentaclePort, "EXPECTED_SERVER_THUMBPRINT")
                {
                    PollingRequestQueueTimeout = TimeSpan.FromSeconds(5)
                });

                Action action = () => echo.SayHello("World");

                action.Should().Throw<HalibutClientException>()
                    .WithMessage(@"* We expected the server to present a certificate with the thumbprint 'EXPECTED_SERVER_THUMBPRINT'. Instead, it presented a certificate with a thumbprint of '36F35047CE8B000CF4C671819A2DD1AFCDE3403D' and subject 'C=""CN=Halibut Alice"", O=""CN=Halibut Alice"", CN=""CN=Halibut Alice"", E=""""'*");
            }
        }

        [Test]
        public void FailWhenPollingAndClientPresentsWrongCertificate()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri($"https://localhost:{octopusPort}"), "EXPECTED_CLIENT_THUMBPRINT"));

                var echo = octopus.CreateClient<IEchoService>(new ServiceEndPoint("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint)
                {
                    PollingRequestQueueTimeout = TimeSpan.FromSeconds(5)
                });

                Action action = () => echo.SayHello("World");

                action.Should().Throw<HalibutClientException>()
                    .WithMessage("*A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time*");
            }
        }

        [Test]
        public void FailWhenPollingAndServerPresentsWrongCertificate()
        {
            var services = GetStubDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust("EXPECTED_SERVER_THUMBPRINT");

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri($"https://localhost:{octopusPort}"), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>(new ServiceEndPoint("poll://SQ-TENTAPOLL", "EXPECTED_SERVER_THUMBPRINT")
                {
                    PollingRequestQueueTimeout = TimeSpan.FromSeconds(5)
                });

                Action action = () => echo.SayHello("World");

                action.Should().Throw<HalibutClientException>()
                    .WithMessage("*A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time*");
            }
        }

#if SUPPORTS_WEB_SOCKET_CLIENT
        [Test]
        public void FailWhenWebSocketPollingAndClientPresentsWrongCertificate()
        {
            var services = GetStubDelegateServiceFactory();

            using (var prereq = new WebSocketListeningPrerequisites())
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                octopus.ListenWebSocket($"https://localhost:{prereq.Port}");
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri($"wss://localhost:{prereq.Port}"), "EXPECTED_SSL_THUMBPRINT"));

                var echo = octopus.CreateClient<IEchoService>(new ServiceEndPoint("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint)
                {
                    PollingRequestQueueTimeout = TimeSpan.FromSeconds(5)
                });

                Action action = () => echo.SayHello("World");

                action.Should().Throw<HalibutClientException>()
                    .WithMessage("*A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time*");
            }
        }

        [Test]
        public void FailWhenWebSocketPollingAndServerPresentsWrongCertificate()
        {
            var services = GetStubDelegateServiceFactory();

            using (var prereq = new WebSocketListeningPrerequisites())
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                octopus.ListenWebSocket($"https://localhost:{prereq.Port}");
                octopus.Trust("EXPECTED_CLIENT_THUMBPRINT");

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri($"wss://localhost:{prereq.Port}"), Certificates.SslThumbprint));

                var echo = octopus.CreateClient<IEchoService>(new ServiceEndPoint("poll://SQ-TENTAPOLL", "EXPECTED_CLIENT_THUMBPRINT")
                {
                    PollingRequestQueueTimeout = TimeSpan.FromSeconds(5)
                });

                Action action = () => echo.SayHello("World");

                action.Should().Throw<HalibutClientException>()
                    .WithMessage("*A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time*");
            }
        }
#endif

        [Test]
        public void FailWhenServerThrowsDuringADataStreamOnListening()
        {
            var services = new DelegateServiceFactory();
            services.Register<IReadDataSteamService>(() => new ReadDataStreamService());

            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var readDataSteamService = octopus.CreateClient<IReadDataSteamService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                // Previously tentacle would eventually stop responding only after many failed calls.
                // This loop ensures (at the time) the test shows the problem.
                for (int i = 0; i < 128; i++)
                {
                    Assert.Throws<HalibutClientException>(() => readDataSteamService.SendData(new DataStream(10000, stream => throw new Exception("Oh noes"))));
                }

                long recieved = readDataSteamService.SendData(DataStream.FromString("hello"));
                recieved.Should().Be(5);
            }
        }

        [Test]
        public void FailWhenServerThrowsDuringADataStreamOnPolling()
        {
            var services = new DelegateServiceFactory();
            services.Register<IReadDataSteamService>(() => new ReadDataStreamService());

            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                var readDataSteamService = octopus.CreateClient<IReadDataSteamService>("poll://SQ-TENTAPOLL", Certificates.TentacleListeningPublicThumbprint);

                Assert.Throws<HalibutClientException>(() => readDataSteamService.SendData(new DataStream(10000, stream => throw new Exception("Oh noes"))));

                long recieved = readDataSteamService.SendData(DataStream.FromString("hello"));
                recieved.Should().Be(5);
            }
        }
    }
}
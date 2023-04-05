using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    [Parallelizable(ParallelScope.All)]
    public class PollingClientConnectionHandlingFixture
    {
        [Test]
        public void PollingClientShouldConnectQuickly()
        {
            var started = DateTime.UtcNow;
            var calls = new List<DateTime>();

            var (server, tentacle, portForwarder, _, doSomeActionService) = SetupPollingServerAndTentacle(() =>
            {
                calls.Add(DateTime.UtcNow);
            });

            using (server)
            using (tentacle)
            using (portForwarder)
            {
                doSomeActionService.Action();
            }

            calls.Should().HaveCount(1);
            calls.ElementAt(0).Should().BeOnOrAfter(started).And.BeCloseTo(started, TimeSpan.FromSeconds(5));
        }

        [Test]
        public void PollingClientShouldReConnectQuickly()
        {
            var started = DateTime.UtcNow;
            var calls = new List<DateTime>();

            var (server, tentacle, portForwarder, _, doSomeActionService) = SetupPollingServerAndTentacle(() =>
            {
                calls.Add(DateTime.UtcNow);
            });

            using (server)
            using (tentacle)
            using (portForwarder)
            {
                doSomeActionService.Action();

                ShutdownPortForwarder(portForwarder);
                ReCreatePortForwarder(portForwarder, tentacle, server);

                try
                {
                    doSomeActionService.Action();
                }
                catch (HalibutClientException ex) when (ex.Message.Contains("An established connection was aborted by the software in your host machine"))
                {
                    // Work around the known dequeue to a broken tcp connection issue
                    doSomeActionService.Action();
                }
            }

            calls.Should().HaveCount(2);
            calls.ElementAt(0).Should().BeOnOrAfter(started).And.BeCloseTo(started, TimeSpan.FromSeconds(5));
            calls.ElementAt(0).Should().BeOnOrAfter(calls.ElementAt(0)).And.BeCloseTo(calls.ElementAt(0), TimeSpan.FromSeconds(5));
        }

        (HalibutRuntime server,
            IHalibutRuntime tentacle,
            PortForwarder portForwarder,
            IEchoService echoService,
            IDoSomeActionService doSomeActionService)
            SetupPollingServerAndTentacle(Action doSomeActionServiceAction)
        {
            var server = SetupServer();
            var portForwarder = SetupPortForwarder(server);
            var serverUri = new Uri("https://localhost:" + portForwarder.PublicEndpoint.Port);
            var tentacle = SetupPollingTentacle(serverUri, "poll://SQ-TENTAPOLL", doSomeActionServiceAction);
            var (echoService, doSomeActionService) = SetupServices(server, "poll://SQ-TENTAPOLL");
            EnsureTentacleIsConnected(echoService);
            return (server, tentacle, portForwarder, echoService, doSomeActionService);
        }

        static HalibutRuntime SetupPollingTentacle(Uri url, string pollingEndpoint, Action doSomeServiceAction)
        {
            var services = new DelegateServiceFactory();
            var doSomeActionService = new DoSomeActionService(doSomeServiceAction);

            services.Register<IDoSomeActionService>(() => doSomeActionService);
            services.Register<IEchoService>(() => new EchoService());

            var pollingTentacle = new HalibutRuntimeBuilder()
                .WithServiceFactory(services)
                .WithServerCertificate(Certificates.TentaclePolling)
                .Build();

            pollingTentacle.Poll(new Uri(pollingEndpoint), new ServiceEndPoint(url, Certificates.OctopusPublicThumbprint));

            return pollingTentacle;
        }

        static PortForwarder SetupPortForwarder(IHalibutRuntime halibut, int? listeningPort = null)
        {
            var octopusPort = halibut.Listen();
            var portForwarder = new PortForwarder(new Uri("https://localhost:" + (octopusPort)), TimeSpan.Zero, listeningPort);

            return portForwarder;
        }

        static HalibutRuntime SetupServer()
        {
            var octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Octopus)
                .Build();

            octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

            return octopus;
        }

        static (IEchoService scriptService, IDoSomeActionService doSomeActionService) SetupServices(HalibutRuntime octopus, string endpoint)
        {
            var doSomeActionService = octopus.CreateClient<IDoSomeActionService>(endpoint, Certificates.TentaclePollingPublicThumbprint);
            var scriptService = octopus.CreateClient<IEchoService>(endpoint, Certificates.TentaclePollingPublicThumbprint);
            return (scriptService, doSomeActionService);
        }

        static void EnsureTentacleIsConnected(IEchoService echoService)
        {
            // Ensure the tentacle is connected
            echoService.SayHello("Hello");
            // Ensure the tentacle is waiting for the next request
            Thread.Sleep(2);
        }

        static PortForwarder ReCreatePortForwarder(PortForwarder portForwarder, IHalibutRuntime tentacle, IHalibutRuntime server)
        {
            var recreatedPortForwarder = SetupPortForwarder(server, portForwarder.PublicEndpoint.Port);

            return recreatedPortForwarder;
        }

        static void ShutdownPortForwarder(PortForwarder portForwarder)
        {
            portForwarder.Dispose();
        }
    }
}
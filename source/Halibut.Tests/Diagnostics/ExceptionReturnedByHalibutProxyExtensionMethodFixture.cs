using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using Halibut.Transport.Proxy;
using NUnit.Framework;

namespace Halibut.Tests.Diagnostics
{
    public static class ExceptionReturnedByHalibutProxyExtensionMethodFixture
    {
        public class WhenTheHalibutProxyThrowsAnException
        {
            [Test]
            public async Task WhenTheConnectionTerminatesWaitingForAResponseFromAPollingTentacle()
            {
                var services = new DelegateServiceFactory();
                DoSomeActionService doSomeActionService = new DoSomeActionService();
                services.Register<IDoSomeActionService>(() => doSomeActionService);
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                {
                    var octopusPort = octopus.Listen();
                    using (var portForwarder = new PortForwarder(new Uri("https://localhost:" + octopusPort), TimeSpan.Zero))
                    using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                    {
                        octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                        tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + portForwarder.PublicEndpoint.Port), Certificates.OctopusPublicThumbprint));

                        var svc = octopus.CreateClient<IDoSomeActionService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                        doSomeActionService.ActionDelegate = () => portForwarder.Dispose();

                        // When svc.Action() is executed, tentacle will kill the TCP connection and dispose the port forwarder preventing new connections.
                        Assert.Throws<HalibutClientException>(() => svc.Action())
                            .IsNetworkError()
                            .Should()
                            .Be(HalibutNetworkExceptionType.UnknownError, "Since currently we get a message envelope is null message");
                    }
                }
            }

            [Test]
            public async Task BecauseThePollingRequestWasNotCollected()
            {
                var services = new DelegateServiceFactory();
                services.Register<IEchoService>(() => new EchoService());
                using (var octopus = new HalibutRuntimeBuilder()
                           .WithServerCertificate(Certificates.Octopus)
                           .Build())
                {
                    octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                    var serviceEndpoint = new ServiceEndPoint("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                    serviceEndpoint.PollingRequestQueueTimeout = TimeSpan.FromSeconds(1);

                    var echo = octopus.CreateClient<IEchoService>(serviceEndpoint);

                    Assert.Throws<HalibutClientException>(() => echo.SayHello("Hello"))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.UnknownError);
                }
            }

            [Test]
            public void BecauseTheTentacleIsNotResponding()
            {
                var services = new DelegateServiceFactory();
                services.Register<IEchoService>(() => new EchoService());

                using (var tcpKiller = new TCPListenerWhichKillsNewConnections())
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                {
                    var serviceEndPoint = new ServiceEndPoint(new Uri("https://localhost:" + tcpKiller.Port), Certificates.TentacleListeningPublicThumbprint);
                    serviceEndPoint.RetryCountLimit = 1;
                    var echo = octopus.CreateClient<IEchoService>(serviceEndPoint);
                    Assert.Throws<HalibutClientException>(() => echo.SayHello("Hello"))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.IsNetworkError);
                }
            }

            [Test]
            public void BecauseTheProxyIsNotResponding_TheExceptionShouldBeANetworkError()
            {
                var services = new DelegateServiceFactory();
                services.Register<IEchoService>(() => new EchoService());

                using (var tcpKiller = new TCPListenerWhichKillsNewConnections())
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                {
                    var serviceEndPoint = new ServiceEndPoint(
                        new Uri("https://localhost:" + tcpKiller.Port),
                        Certificates.TentacleListeningPublicThumbprint,
                        new ProxyDetails("127.0.0.1", tcpKiller.Port, ProxyType.HTTP));

                    serviceEndPoint.RetryCountLimit = 1;

                    var echo = octopus.CreateClient<IEchoService>(serviceEndPoint);

                    Assert.Throws<HalibutClientException>(() => echo.SayHello("Hello"))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.IsNetworkError);
                }
            }
        }
    }
}
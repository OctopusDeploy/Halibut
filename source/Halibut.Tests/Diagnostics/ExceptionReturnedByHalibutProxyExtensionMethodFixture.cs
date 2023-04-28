using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Halibut.Tests.Diagnostics;

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

                    try
                    {
                        // When svc.Action() is executed, tentacle will kill the TCP connection and dispose the port forwarder preventing new connections.
                        svc.Action();
                    }
                    catch (Exception exception)
                    {
                        exception.IsNetworkError()
                            .Should()
                            .Be(HalibutNetworkExceptionType.UnknownError, "Since currently we get a message envelope is null message");
                    }
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

                try
                {
                    echo.SayHello("Hello");
                }
                catch (Exception exception)
                {
                    exception.IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.UnknownError, "We don't know why it wasn't collected.");
                }
            }
        }
        
        [Test]
        public void BecauseTheTentacleIsNotListening_TheError()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());

            int tentaclePort = 0;
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);
            }
            
            using (var octopus = new HalibutRuntime(Certificates.Octopus)) {
                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                try
                {
                    echo.SayHello("Hello");
                }
                catch (Exception exception)
                {
                    exception.IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.IsNetworkError);
                }

            }
        }
        
    }
}
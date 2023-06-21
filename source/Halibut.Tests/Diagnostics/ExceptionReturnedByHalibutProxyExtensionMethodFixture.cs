using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using Halibut.Transport.Proxy;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Diagnostics
{
    public static class ExceptionReturnedByHalibutProxyExtensionMethodFixture
    {
        public class WhenGivenA
        {
            [Test]
            public void MethodNotFoundHalibutClientException_ItIsNotANetworkError()
            {
                new MethodNotFoundHalibutClientException("").IsNetworkError()
                    .Should()
                    .Be(HalibutNetworkExceptionType.NotANetworkError);
            }
            
            [Test]
            public void ServiceNotFoundHalibutClientException_ItIsNotANetworkError()
            {
                new ServiceNotFoundHalibutClientException("").IsNetworkError()
                    .Should()
                    .Be(HalibutNetworkExceptionType.NotANetworkError);
            }
            
            [Test]
            public void AmbiguousMethodMatchHalibutClientException_ItIsNotANetworkError()
            {
                new AmbiguousMethodMatchHalibutClientException("").IsNetworkError()
                    .Should()
                    .Be(HalibutNetworkExceptionType.NotANetworkError);
            }
        }
        public class WhenTheHalibutProxyThrowsAnException
        {
            [Test]
            public async Task WhenTheConnectionTerminatesWaitingForAResponseFromAPollingTentacle()
            {
                var services = new DelegateServiceFactory();
                DoSomeActionService doSomeActionService = new DoSomeActionService();
                services.Register<IDoSomeActionService>(() => doSomeActionService);
                
                using (var clientAndService = ClientServiceBuilder.Listening().WithServiceFactory(services).WithPortForwarding().Build())
                {
                    var svc = clientAndService.CreateClient<IDoSomeActionService>();

                    doSomeActionService.ActionDelegate = () => clientAndService.portForwarder.Dispose();

                    // When svc.Action() is executed, tentacle will kill the TCP connection and dispose the port forwarder preventing new connections.
                    Assert.Throws<HalibutClientException>(() => svc.Action())
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.UnknownError, "Since currently we get a message envelope is null message");
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
                using (var clientAndService = ClientServiceBuilder.Listening().NoService().Build())
                {
                    var echo = clientAndService.CreateClient<IEchoService>(serviceEndPoint => { serviceEndPoint.RetryCountLimit = 1; });

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
            
            [Test]
            public void BecauseOfAInvalidCertificateException_ItIsNotANetworkError()
            {
                var services = new DelegateServiceFactory();
                services.Register<IEchoService>(() => new EchoService());
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
                {
                    var tentaclePort = tentacleListening.Listen();
                    tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                    var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, "Wrong Thumbrprint");

                    Assert.Throws<HalibutClientException>(() => echo.SayHello("Hello"))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }
            
            [Test]
            public void BecauseTheDataStreamHadAnErrorOpeningTheFileWithFileStream_WhenSendingToListening_ItIsNotANetworkError()
            {
                var services = new DelegateServiceFactory();
                services.Register<IEchoService>(() => new EchoService());
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
                {
                    var tentaclePort = tentacleListening.Listen();
                    tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                    var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                    var dataStream = new DataStream(10, stream =>
                    {
                        new FileStream("DoesNotExist2497546", FileMode.Open).Dispose();
                    });
                    Assert.Throws<HalibutClientException>(() => echo.CountBytes(dataStream))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }
            
            [Test]
            public void BecauseTheDataStreamHadAnErrorOpeningTheFileWithFileStream_WhenSendingToPolling_ItIsNotANetworkError()
            {
                var services = new DelegateServiceFactory();
                services.Register<IEchoService>(() => new EchoService());
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                {
                    var octopusPort = octopus.Listen();
                    octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                    tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                    var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                    var dataStream = new DataStream(10, stream =>
                    {
                        new FileStream("DoesNotExist2497546", FileMode.Open).Dispose();
                    });
                    Assert.Throws<HalibutClientException>(() => echo.CountBytes(dataStream))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }
            
            [Test]
            public void BecauseTheDataStreamThrowAFileNotFoundException_WhenSendingToPolling_ItIsNotANetworkError()
            {
                var services = new DelegateServiceFactory();
                services.Register<IEchoService>(() => new EchoService());
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                {
                    var octopusPort = octopus.Listen();
                    octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                    tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                    var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                    var dataStream = new DataStream(10, stream =>
                    {
                        throw new FileNotFoundException();
                    });
                    Assert.Throws<HalibutClientException>(() => echo.CountBytes(dataStream))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }
            
            [Test]
            public void BecauseTheServiceThrowAnException_ItIsNotANetworkError()
            {
                var services = new DelegateServiceFactory();
                services.Register<IEchoService>(() => new EchoService());
                using (var octopus = new HalibutRuntime(Certificates.Octopus))
                using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
                {
                    var octopusPort = octopus.Listen();
                    octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                    tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                    var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                    
                    var exception = Assert.Throws<ServiceInvocationHalibutClientException>(() => echo.Crash());
                    exception.IsNetworkError().Should().Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }
        }
    }
}
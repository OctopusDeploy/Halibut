using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
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
                using (var clientAndService = ClientServiceBuilder
                           .Polling()
                           .WithPortForwarding(out var portForwarder)
                           .WithDoSomeActionService(() => portForwarder.Value.EnterKillNewAndExistingConnectionsMode())
                           .Build())
                {
                    var svc = clientAndService.CreateClient<IDoSomeActionService>();

                    // When svc.Action() is executed, tentacle will kill the TCP connection and dispose the port forwarder preventing new connections.
                    var exception = Assert.Throws<HalibutClientException>(() => svc.Action());
                    new SerilogLoggerBuilder().Build().Information(exception, "Got an exception, we were expecting one");
                    exception.IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.UnknownError, "Since currently we get a message envelope is null message");
                }
            }

            [Test]
            public async Task BecauseThePollingRequestWasNotCollected()
            {
                var services = new DelegateServiceFactory();
                services.Register<IEchoService>(() => new EchoService());

                using (var clientAndService = ClientServiceBuilder
                           .Polling()
                           .NoService()
                           .Build())
                {
                    var echo = clientAndService.CreateClient<IEchoService>(point => point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(1));

                    Assert.Throws<HalibutClientException>(() => echo.SayHello("Hello"))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.UnknownError);
                }
            }

            [Test]
            public void BecauseTheListeningTentacleIsNotResponding()
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
            public void BecauseOfAInvalidCertificateException_WhenConnectingToListening_ItIsNotANetworkError()
            {
                using (var clientAndService = ClientServiceBuilder
                           .Listening()
                           .WithEchoService()
                           .Build())
                {
                    var echo = clientAndService.CreateClient<IEchoService>(remoteThumbprint: "Wrong Thumbrprint");

                    Assert.Throws<HalibutClientException>(() => echo.SayHello("Hello"))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }

            [Test]
            [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
            public void BecauseTheDataStreamHadAnErrorOpeningTheFileWithFileStream_WhenSending_ItIsNotANetworkError(ServiceConnectionType serviceConnectionType)
            {
                using (var clientAndService = ClientServiceBuilder
                           .ForMode(serviceConnectionType)
                           .WithEchoService()
                           .Build())
                {
                    var echo = clientAndService.CreateClient<IEchoService>();

                    var dataStream = new DataStream(10, _ => new FileStream("DoesNotExist2497546", FileMode.Open).Dispose());

                    Assert.Throws<HalibutClientException>(() => echo.CountBytes(dataStream))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }

            [Test]
            [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
            public void BecauseTheDataStreamThrowAFileNotFoundException_WhenSending_ItIsNotANetworkError(ServiceConnectionType serviceConnectionType)
            {
                using (var clientAndService = ClientServiceBuilder
                           .ForMode(serviceConnectionType)
                           .WithEchoService()
                           .Build())
                {
                    var echo = clientAndService.CreateClient<IEchoService>();

                    var dataStream = new DataStream(10, _ => throw new FileNotFoundException());

                    Assert.Throws<HalibutClientException>(() => echo.CountBytes(dataStream))
                        .IsNetworkError()
                        .Should()
                        .Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }

            [Test]
            [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
            public void BecauseTheServiceThrowAnException_ItIsNotANetworkError(ServiceConnectionType serviceConnectionType)
            {
                using (var clientAndService = ClientServiceBuilder
                           .ForMode(serviceConnectionType)
                           .WithEchoService()
                           .Build())
                {
                    var echo = clientAndService.CreateClient<IEchoService>();

                    var exception = Assert.Throws<ServiceInvocationHalibutClientException>(() => echo.Crash());
                    exception.IsNetworkError().Should().Be(HalibutNetworkExceptionType.NotANetworkError);
                }
            }
        }
    }
}
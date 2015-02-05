using System;
using System.Diagnostics;
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
                Assert.That(info.RemoteThumbprint, Is.EqualTo(Certificates.TentacleListeningPublicThumbprint));
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
                    Assert.That(echo.SayHello("Deploy package A"), Is.EqualTo("Deploy package A..."));
                }
            }
        }

        [Test]
        public void MessagesCanBeRouted()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var router = new HalibutRuntime(services, Certificates.TentaclePolling))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var routerPort = router.Listen();

                router.Trust(Certificates.OctopusPublicThumbprint);

                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.TentaclePollingPublicThumbprint);

                octopus.Route(
                    to: new ServiceEndPoint(new Uri("https://localhost:" + tentaclePort), Certificates.TentacleListeningPublicThumbprint),
                    via: new ServiceEndPoint(new Uri("https://localhost:" + routerPort), Certificates.TentaclePollingPublicThumbprint)
                    );

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                for (var i = 0; i < 100; i++)
                {
                    Assert.That(echo.SayHello("Deploy package A"), Is.EqualTo("Deploy package A..."));
                }
            }
        }

        [Test]
        public void StreamsCanBeSent()
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
    }
}
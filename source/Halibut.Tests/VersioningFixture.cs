using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class VersioningFixture
    {
        [Test]
        public void ServerV1AndListeningTentacleV1ShouldTalk()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());

            using (var server = new HalibutRuntime(Certificates.Octopus))
            using (var tentacle = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                server.justForTesting = new Version(1, 0);
                tentacle.justForTesting = new Version(1, 0);

                var tentaclePort = tentacle.Listen();
                tentacle.Trust(Certificates.OctopusPublicThumbprint);

                var echo = server.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");
            }
        }

        [Test]
        public void ServerV1AndListeningTentacleV2ShouldTalk()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());

            using (var server = new HalibutRuntime(Certificates.Octopus))
            using (var tentacle = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                server.justForTesting = new Version(1, 0);
                tentacle.justForTesting = new Version(2, 0);

                var tentaclePort = tentacle.Listen();
                tentacle.Trust(Certificates.OctopusPublicThumbprint);

                var echo = server.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");
            }
        }

        [Test]
        public void ServerV2AndListeningTentacleV1ShouldTalk()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());

            using (var server = new HalibutRuntime(Certificates.Octopus))
            using (var tentacle = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                server.justForTesting = new Version(2, 0);
                tentacle.justForTesting = new Version(1, 0);

                var tentaclePort = tentacle.Listen();
                tentacle.Trust(Certificates.OctopusPublicThumbprint);

                var echo = server.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");
            }
        }

        [Test]
        public void ServerV2AndListeningTentacleV2ShouldTalk()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());

            using (var server = new HalibutRuntime(Certificates.Octopus))
            using (var tentacle = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                server.justForTesting = new Version(2, 0);
                tentacle.justForTesting = new Version(2, 0);

                var tentaclePort = tentacle.Listen();
                tentacle.Trust(Certificates.OctopusPublicThumbprint);

                var echo = server.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");
            }
        }
    }
}
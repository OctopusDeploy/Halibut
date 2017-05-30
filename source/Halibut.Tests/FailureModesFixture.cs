using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Xunit;
using Xunit.Abstractions;

namespace Halibut.Tests
{
    public class FailureModesFixture
    {
        DelegateServiceFactory services;

        public FailureModesFixture(ITestOutputHelper output)
        {
            LogProvider.SetCurrentLogProvider(new XunitLogProvider(output));
            services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
        }

        [Fact]
        public async Task FailsWhenSendingToPollingMachineButNothingPicksItUp()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                var error = await Assert.ThrowsAsync<HalibutClientException>(() => echo.SayHello("Paul")).ConfigureAwait(false);
                error.Message.Should().Contain("the polling endpoint did not collect the request within the allowed time");

                await octopus.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task FailWhenServerThrowsAnException()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                var ex = await Assert.ThrowsAsync<HalibutClientException>(() => echo.Crash()).ConfigureAwait(false);
                ex.Message.Should().Contain("at Halibut.Tests.TestServices.EchoService.Crash()").And.Contain("divide by zero");

                await octopus.Stop().ConfigureAwait(false);
                await tentacleListening.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task FailWhenServerThrowsAnExceptionOnPolling()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                var ex = await Assert.ThrowsAsync<HalibutClientException>(() => echo.Crash()).ConfigureAwait(false);
                ex.Message.Should().Contain("at Halibut.Tests.TestServices.EchoService.Crash()").And.Contain("divide by zero");

                await octopus.Stop().ConfigureAwait(false);
                await tentaclePolling.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task FailOnInvalidHostnameAsync()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000", Certificates.TentacleListeningPublicThumbprint);
                var ex = await Assert.ThrowsAsync<HalibutClientException>(() => echo.Crash()).ConfigureAwait(false);
                ex.Message.Should().Contain("when sending a request to 'https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000/', before the request").And.Contain("No such host is known");

                await octopus.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task FailOnInvalidPort()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("https://google.com:88", Certificates.TentacleListeningPublicThumbprint);
                var ex = await Assert.ThrowsAsync<HalibutClientException>(() => echo.Crash()).ConfigureAwait(false);
                ex.Message.Should().Contain("when sending a request to 'https://google.com:88/', before the request");

                await octopus.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task FailWhenListeningClientPresentsWrongCertificate()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.TentaclePolling))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                await Assert.ThrowsAsync<HalibutClientException>(() => echo.SayHello("World")).ConfigureAwait(false);

                await octopus.Stop().ConfigureAwait(false);
                await tentacleListening.Stop().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task FailWhenListeningServerPresentsWrongCertificate()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentaclePollingPublicThumbprint);

                await Assert.ThrowsAsync<HalibutClientException>(() => echo.SayHello("World")).ConfigureAwait(false);

                await octopus.Stop().ConfigureAwait(false);
                await tentacleListening.Stop().ConfigureAwait(false);
            }
        }
    }
}
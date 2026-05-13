using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics.LogCreators;
using Halibut.Tests.Support;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    /// <summary>
    /// Proves that process isolation prevents SChannel session cache collisions.
    ///
    /// In-process, a single <see cref="HalibutRuntime"/> acting as both TLS server and TLS
    /// client to localhost with two different certificates can collide in the SChannel session
    /// cache (Windows). Running the tentacle in a separate process avoids this because the
    /// SChannel session cache is per-process.
    ///
    /// These tests verify that two separate processes — one listening tentacle and one polling
    /// tentacle — both using the same certificate and both connecting via localhost, can
    /// simultaneously communicate successfully with an in-process Octopus server.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class SchannelSessionCacheFixture : BaseTest
    {
        [Test]
        public async Task ListeningTentacleInSeparateProcessCanCommunicateWithOctopus()
        {
            using var tmpDirectory = new TmpDirectory();
            var octopusCert = CertificateGenerator.GenerateSelfSignedCertificate(tmpDirectory.FullPath);
            var tentacleCert = CertificateGenerator.GenerateSelfSignedCertificate(tmpDirectory.FullPath);

            var octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(octopusCert.Certificate2)
                .WithLogFactory(new TestContextLogCreator("Octopus", Logging.LogLevel.Trace).ToCachingLogFactory())
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();

            await using var _ = new AsyncDisposableAction(async () => await octopus.DisposeAsync());

            octopus.Trust(tentacleCert.Thumbprint);

            using var runningTentacle = await new SchannelProbeBinaryRunner(
                ServiceConnectionType.Listening,
                clientListenPort: null,
                clientCertAndThumbprint: octopusCert,
                serviceCertAndThumbprint: tentacleCert,
                logger: Logger).Run();

            runningTentacle.ServiceListenPort.Should().NotBeNull("listening tentacle should have reported its port");

            var serviceUri = new Uri($"https://localhost:{runningTentacle.ServiceListenPort}");
            var serviceEndPoint = new ServiceEndPoint(serviceUri, tentacleCert.Thumbprint, octopus.TimeoutsAndLimits);

            var echo = octopus.CreateAsyncClient<ISayHelloService, IAsyncClientSayHelloService>(serviceEndPoint);
            var result = await echo.SayHelloAsync("world");

            result.Should().Be("world...");
        }

        [Test]
        public async Task PollingTentacleInSeparateProcessCanCommunicateWithOctopus()
        {
            using var tmpDirectory = new TmpDirectory();
            var octopusCert = CertificateGenerator.GenerateSelfSignedCertificate(tmpDirectory.FullPath);
            var tentacleCert = CertificateGenerator.GenerateSelfSignedCertificate(tmpDirectory.FullPath);

            var octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(octopusCert.Certificate2)
                .WithLogFactory(new TestContextLogCreator("Octopus", Logging.LogLevel.Trace).ToCachingLogFactory())
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();

            await using var _ = new AsyncDisposableAction(async () => await octopus.DisposeAsync());

            octopus.Trust(tentacleCert.Thumbprint);
            var pollingListenPort = octopus.Listen();

            using var runningTentacle = await new SchannelProbeBinaryRunner(
                ServiceConnectionType.Polling,
                clientListenPort: pollingListenPort,
                clientCertAndThumbprint: octopusCert,
                serviceCertAndThumbprint: tentacleCert,
                logger: Logger).Run();

            var serviceUri = new Uri("poll://SQ-TENTAPOLL");
            var serviceEndPoint = new ServiceEndPoint(serviceUri, tentacleCert.Thumbprint, octopus.TimeoutsAndLimits);

            var echo = octopus.CreateAsyncClient<ISayHelloService, IAsyncClientSayHelloService>(serviceEndPoint);
            var result = await echo.SayHelloAsync("world");

            result.Should().Be("world...");
        }

        [Test]
        public async Task ListeningAndPollingTentaclesInSeparateProcessesCanSimultaneouslyCommunicateWithOctopus()
        {
            using var tmpDirectory = new TmpDirectory();
            var octopusCert = CertificateGenerator.GenerateSelfSignedCertificate(tmpDirectory.FullPath);
            // Both tentacles intentionally share the same certificate to maximise the chance of
            // triggering an SChannel session-cache collision if process isolation were absent.
            var sharedTentacleCert = CertificateGenerator.GenerateSelfSignedCertificate(tmpDirectory.FullPath);

            var octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(octopusCert.Certificate2)
                .WithLogFactory(new TestContextLogCreator("Octopus", Logging.LogLevel.Trace).ToCachingLogFactory())
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();

            await using var _ = new AsyncDisposableAction(async () => await octopus.DisposeAsync());

            octopus.Trust(sharedTentacleCert.Thumbprint);
            var pollingListenPort = octopus.Listen();

            // Start listening tentacle
            using var listeningTentacle = await new SchannelProbeBinaryRunner(
                ServiceConnectionType.Listening,
                clientListenPort: null,
                clientCertAndThumbprint: octopusCert,
                serviceCertAndThumbprint: sharedTentacleCert,
                logger: Logger).Run();

            listeningTentacle.ServiceListenPort.Should().NotBeNull("listening tentacle should have reported its port");

            // Start polling tentacle
            using var pollingTentacle = await new SchannelProbeBinaryRunner(
                ServiceConnectionType.Polling,
                clientListenPort: pollingListenPort,
                clientCertAndThumbprint: octopusCert,
                serviceCertAndThumbprint: sharedTentacleCert,
                logger: Logger).Run();

            var listeningServiceUri = new Uri($"https://localhost:{listeningTentacle.ServiceListenPort}");
            var listeningEndPoint = new ServiceEndPoint(listeningServiceUri, sharedTentacleCert.Thumbprint, octopus.TimeoutsAndLimits);

            var pollingServiceUri = new Uri("poll://SQ-TENTAPOLL");
            var pollingEndPoint = new ServiceEndPoint(pollingServiceUri, sharedTentacleCert.Thumbprint, octopus.TimeoutsAndLimits);

            var listeningEcho = octopus.CreateAsyncClient<ISayHelloService, IAsyncClientSayHelloService>(listeningEndPoint);
            var pollingEcho = octopus.CreateAsyncClient<ISayHelloService, IAsyncClientSayHelloService>(pollingEndPoint);

            // Call both simultaneously
            var listeningTask = listeningEcho.SayHelloAsync("from-listening");
            var pollingTask = pollingEcho.SayHelloAsync("from-polling");

            var results = await Task.WhenAll(listeningTask, pollingTask);

            results[0].Should().Be("from-listening...");
            results[1].Should().Be("from-polling...");
        }
    }

    class AsyncDisposableAction : IAsyncDisposable
    {
        readonly Func<Task> action;

        public AsyncDisposableAction(Func<Task> action)
        {
            this.action = action;
        }

        public async ValueTask DisposeAsync()
        {
            await action();
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    /// <summary>
    /// A polling client is trusted by its certificate, but chooses which subscription it polls. These tests make sure a
    /// trusted client cannot collect requests meant for a polling client with a different certificate.
    /// </summary>
    public class PollingSubscriptionCertificateFixture : BaseTest
    {
        [Test]
        public async Task TrustedPollingClientCannotCollectRequestsForASubscriptionThatExpectsADifferentCertificate()
        {
            var subscription = new Uri("poll://tentacle");
            var expectedThumbprint = Certificates.TentaclePollingPublicThumbprint;
            var otherClientThumbprint = Certificates.TentacleListeningPublicThumbprint;

            var otherClientsService = new AsyncCountingService();
            var otherClientsServices = new DelegateServiceFactory();
            otherClientsServices.Register<ICountingService, IAsyncCountingService>(() => otherClientsService);

            await using var server = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Octopus)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();

            // This client uses a certificate the server trusts, but polls a subscription that expects a different certificate.
            await using var otherClient = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.TentacleListening)
                .WithServiceFactory(otherClientsServices)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();

            var serverPort = server.Listen();
            server.Trust(expectedThumbprint);
            server.Trust(otherClientThumbprint);

            otherClient.Poll(subscription, new ServiceEndPoint("https://localhost:" + serverPort, Certificates.OctopusPublicThumbprint, server.TimeoutsAndLimits), CancellationToken);

            var endpoint = new ServiceEndPoint(subscription, expectedThumbprint, server.TimeoutsAndLimits);
            var client = server.CreateAsyncClient<ICountingService, IAsyncClientCountingService>(endpoint);

            var exception = (await AssertException.Throws<PollingClientCertificateMismatchHalibutClientException>(async () => await client.IncrementAsync())).And;

            otherClientsService.CurrentValue().Should().Be(0, "the request must never be sent to a client presenting a different certificate");
            exception.Message.Should().Contain(otherClientThumbprint).And.Contain(expectedThumbprint).And.Contain(subscription.ToString());
            exception.IsRetryableError().Should().Be(HalibutRetryableErrorType.NotRetryable);

            // The rejected request is recorded in the subscription's own log, so it is visible alongside the endpoint's other communication.
            server.Logs.ForEndpoint(subscription).GetLogs()
                .Should().Contain(l => l.Type == EventType.ErrorInIdentify && l.FormattedMessage.Contains(otherClientThumbprint) && l.FormattedMessage.Contains(expectedThumbprint));
        }

        [Test]
        public async Task PollingClientPresentingTheExpectedCertificateReceivesRequests()
        {
            var subscription = new Uri("poll://tentacle");

            var service = new AsyncCountingService();
            var services = new DelegateServiceFactory();
            services.Register<ICountingService, IAsyncCountingService>(() => service);

            await using var server = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Octopus)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();

            await using var tentacle = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.TentaclePolling)
                .WithServiceFactory(services)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();

            var serverPort = server.Listen();
            server.Trust(Certificates.TentaclePollingPublicThumbprint);

            tentacle.Poll(subscription, new ServiceEndPoint("https://localhost:" + serverPort, Certificates.OctopusPublicThumbprint, server.TimeoutsAndLimits), CancellationToken);

            // Thumbprints are compared ignoring case.
            var endpoint = new ServiceEndPoint(subscription, Certificates.TentaclePollingPublicThumbprint.ToLowerInvariant(), server.TimeoutsAndLimits);
            var client = server.CreateAsyncClient<ICountingService, IAsyncClientCountingService>(endpoint);

            await client.IncrementAsync();

            service.CurrentValue().Should().Be(1);
            server.Logs.ForEndpoint(subscription).GetLogs().Should().NotContain(l => l.Type == EventType.ErrorInIdentify);
        }
    }
}

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
        public async Task PollingSubscriptionVerifierStopsATrustedClientWithADifferentCertificateFromConnectingForTheSubscription()
        {
            var subscription = new Uri("poll://tentacle");
            var expectedThumbprint = Certificates.TentaclePollingPublicThumbprint;
            var otherClientThumbprint = Certificates.TentacleListeningPublicThumbprint;

            var expectedClientsService = new AsyncCountingService();
            var expectedClientsServices = new DelegateServiceFactory();
            expectedClientsServices.Register<ICountingService, IAsyncCountingService>(() => expectedClientsService);

            var otherClientsService = new AsyncCountingService();
            var otherClientsServices = new DelegateServiceFactory();
            otherClientsServices.Register<ICountingService, IAsyncCountingService>(() => otherClientsService);

            await using var server = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Octopus)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .WithPollingSubscriptionVerifier(new SingleSubscriptionVerifier(subscription, expectedThumbprint))
                .Build();

            await using var expectedClient = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.TentaclePolling)
                .WithServiceFactory(expectedClientsServices)
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

            var serverEndpoint = new ServiceEndPoint("https://localhost:" + serverPort, Certificates.OctopusPublicThumbprint, server.TimeoutsAndLimits);
            otherClient.Poll(subscription, serverEndpoint, CancellationToken);
            expectedClient.Poll(subscription, serverEndpoint, CancellationToken);

            var endpoint = new ServiceEndPoint(subscription, expectedThumbprint, server.TimeoutsAndLimits);
            var client = server.CreateAsyncClient<ICountingService, IAsyncClientCountingService>(endpoint);

            // Without the verifier, the other client would compete for these requests and make some of them fail.
            const int requestCount = 10;
            for (var i = 0; i < requestCount; i++)
            {
                await client.IncrementAsync();
            }

            expectedClientsService.CurrentValue().Should().Be(requestCount);
            otherClientsService.CurrentValue().Should().Be(0);

            // The other client keeps reconnecting, so its rejection is logged against the subscription, though perhaps not yet.
            await WaitUntil(() => server.Logs.ForEndpoint(subscription).GetLogs()
                .Any(l => l.Type == EventType.ErrorInIdentify && l.FormattedMessage.Contains(otherClientThumbprint) && l.FormattedMessage.Contains(subscription.ToString())));
        }

        async Task WaitUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!condition())
            {
                DateTime.UtcNow.Should().BeBefore(deadline, "the condition should become true");
                await Task.Delay(100, CancellationToken);
            }
        }

        class SingleSubscriptionVerifier : IPollingSubscriptionVerifier
        {
            readonly Uri subscription;
            readonly string thumbprint;

            public SingleSubscriptionVerifier(Uri subscription, string thumbprint)
            {
                this.subscription = subscription;
                this.thumbprint = thumbprint;
            }

            public Task<bool> IsExpectedClientAsync(Uri subscriptionId, string clientThumbprint, System.Threading.CancellationToken cancellationToken)
            {
                return Task.FromResult(subscriptionId == subscription && string.Equals(clientThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}

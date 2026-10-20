using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class PollingSubscriptionThumbprintFixture : BaseTest
    {
        [Test]
        public async Task PollingServiceCannotCollectRequestsForAnotherThumbprint()
        {
            var countingService = new AsyncCountingService();
            var services = new DelegateServiceFactory();
            services.Register<ICountingService, IAsyncCountingService>(() => countingService);

            await using var client = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Octopus)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();
            await using var pollingService = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.TentacleListening)
                .WithServiceFactory(services)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();

            var octopusPort = client.Listen();
            client.Trust(Certificates.TentaclePollingPublicThumbprint);
            client.Trust(Certificates.TentacleListeningPublicThumbprint);

            pollingService.Poll(new Uri("poll://alice"), new ServiceEndPoint("https://localhost:" + octopusPort, Certificates.OctopusPublicThumbprint, client.TimeoutsAndLimits), CancellationToken);

            var serviceEndPoint = new ServiceEndPoint("poll://alice", Certificates.TentaclePollingPublicThumbprint, client.TimeoutsAndLimits)
            {
                PollingRequestQueueTimeout = TimeSpan.FromSeconds(20)
            };
            var counter = client.CreateAsyncClient<ICountingService, IAsyncClientCountingService>(serviceEndPoint);

            var exception = (await AssertException.Throws<HalibutClientException>(counter.IncrementAsync())).And;

            exception.Message.Should().Contain(Certificates.TentacleListeningPublicThumbprint);
            countingService.CurrentValue().Should().Be(0);
        }

        [Test]
        public async Task PollingServiceCollectsRequestsForItsOwnThumbprint()
        {
            var countingService = new AsyncCountingService();
            var services = new DelegateServiceFactory();
            services.Register<ICountingService, IAsyncCountingService>(() => countingService);

            await using var client = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Octopus)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();
            await using var pollingService = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.TentaclePolling)
                .WithServiceFactory(services)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();

            var octopusPort = client.Listen();
            client.Trust(Certificates.TentaclePollingPublicThumbprint);

            pollingService.Poll(new Uri("poll://alice"), new ServiceEndPoint("https://localhost:" + octopusPort, Certificates.OctopusPublicThumbprint, client.TimeoutsAndLimits), CancellationToken);

            var counter = client.CreateAsyncClient<ICountingService, IAsyncClientCountingService>(new ServiceEndPoint("poll://alice", Certificates.TentaclePollingPublicThumbprint, client.TimeoutsAndLimits));

            await counter.IncrementAsync();

            countingService.CurrentValue().Should().Be(1);
        }
    }
}

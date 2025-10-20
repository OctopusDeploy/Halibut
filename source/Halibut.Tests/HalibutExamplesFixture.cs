using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests
{
    public class HalibutExamplesFixture : BaseTest
    {
        [RedisTest]
        public async Task SimplePollingExample()
        {
            var services = GetDelegateServiceFactory();
            await using (var client = new HalibutRuntimeBuilder()
                             .WithServerCertificate(Certificates.Octopus)
                             .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                             .Build())
            await using (var pollingService = new HalibutRuntimeBuilder()
                             .WithServerCertificate(Certificates.TentaclePolling)
                             .WithServiceFactory(services)
                             .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                             .Build())
            {
                var octopusPort = client.Listen();
                client.Trust(Certificates.TentaclePollingPublicThumbprint);
                
                pollingService.Poll(new Uri("poll://alice"), new ServiceEndPoint("https://localhost:" + octopusPort, Certificates.OctopusPublicThumbprint, client.TimeoutsAndLimits), CancellationToken);

                var echo = client.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(new ServiceEndPoint("poll://alice", null, client.TimeoutsAndLimits));

                await echo.SayHelloAsync("World");
            }
        }
        
        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());
            return services;
        }
    }
}
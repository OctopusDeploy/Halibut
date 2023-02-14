using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class AsyncToSyncContractFixture
    {
        public interface IAsyncEchoService
        {
            Task<string> SayHelloAsync(string name);
            string SayHello(string name);
        }

        public interface ISyncEchoService
        {
            string SayHello(string name);
        }

        public class SyncEchoService : ISyncEchoService
        {
            public string SayHello(string name)
            {
                return name;
            }
        }

        [Test]
        public async Task AsyncTestExample()
        {
            var services = new DelegateServiceFactory();
            services.Register<ISyncEchoService>(() => new SyncEchoService());

            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<ISyncEchoService, IAsyncEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                var res = await echo.SayHelloAsync("hello");
                res.Should().Be("hello");
            }
        }
    }
}
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Microsoft.VisualStudio.Threading;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class BiDirectionalTests : BaseTest
    {
        /// <summary>
        /// Test is redundant but kept around since we really want the security part to work. 
        /// </summary>
        [Test]
        public async Task ClientCanRespondToServerRequests()
        {
            await SetupBiDirectionalClients(async (octopusEchoClient, tentacleEchoClient) =>
            {
                var octopusResponse = await octopusEchoClient.SayHelloAsync("Hello");
                var tentacleResponse = await tentacleEchoClient.SayHelloAsync("World");

                octopusResponse.Should().Be("Hello...");
                tentacleResponse.Should().Be("World...");
            });
        }

        [Test]
        public async Task ConcurrentTasksDoNotBlock()
        {
            await SetupBiDirectionalClients(async (octopusEchoClient, tentacleEchoClient) =>
            {
                var sw = Stopwatch.StartNew();
                var t1 = Task.Run(async () => await RunLongRunningTask(octopusEchoClient)).WithTimeout(TimeSpan.FromSeconds(12));
                var t2 = Task.Run(async () => await RunLongRunningTask(tentacleEchoClient)).WithTimeout(TimeSpan.FromSeconds(12));

                await Task.WhenAll(t1, t2);
                sw.Stop();

                sw.ElapsedMilliseconds.Should().BeLessThan(await t1 + await t2);
            });
            
            async Task<long> RunLongRunningTask(IAsyncClientEchoService tentacleEchoClient)
            {
                var longRunningDuration = Stopwatch.StartNew();
                await tentacleEchoClient.LongRunningOperationAsync();
                longRunningDuration.Stop();
                return longRunningDuration.ElapsedMilliseconds;
            }
        }

        [Test]
        public async Task ConcurrentClientAndServerRequestsCorrectlyInterleaved()
        {
            await SetupBiDirectionalClients(async (octopusEchoClient, tentacleEchoClient) =>
            {
                var t1 = Task.Run(async () => await RunEchoTask(octopusEchoClient)).WithTimeout(TimeSpan.FromSeconds(5));
                var t2 = Task.Run(async () => await RunEchoTask(tentacleEchoClient)).WithTimeout(TimeSpan.FromSeconds(5));
                
                await Task.WhenAll(t1, t2);

                t1.Status.Should().Be(TaskStatus.RanToCompletion);
                t2.Status.Should().Be(TaskStatus.RanToCompletion);
            });
            
            async Task RunEchoTask(IAsyncClientEchoService tentacleEchoClient)
            {
                 var j = 0;
                 while (j++ < 10)
                 {
                     var tentacleResponse = await tentacleEchoClient.SayHelloAsync($"Hello{j}");
                     tentacleResponse.Should().Be($"Hello{j}...");
                 }
            }
        }
        
        async Task SetupBiDirectionalClients(Func<IAsyncClientEchoService, IAsyncClientEchoService, Task> thing)
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());

            await using var octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Octopus)
                .WithServiceFactory(services)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();
            await using var tentacleListening = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.TentacleListening)
                .WithServiceFactory(services)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();
            
            var tentaclePort = tentacleListening.Listen(); 
            tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

            octopus.Poll(new Uri("poll://foobar"), new ServiceEndPoint("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint, octopus.TimeoutsAndLimits), CancellationToken.None);
                
            var octopusEchoClient = octopus.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(new ServiceEndPoint("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint, octopus.TimeoutsAndLimits));
            var tentacleEchoClient = tentacleListening.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(new ServiceEndPoint(new Uri("poll://foobar"), Certificates.TentacleListeningPublicThumbprint, octopus.TimeoutsAndLimits));
                
            await thing(octopusEchoClient, tentacleEchoClient);
        }
    }
}
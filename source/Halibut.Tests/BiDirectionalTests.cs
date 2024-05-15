using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Microsoft.VisualStudio.Threading;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class BiDirectionalTests : BaseTest
    {
        readonly X509Certificate2 _endpointACertificate = Certificates.Octopus;
        readonly X509Certificate2 _endpointBCertificate = Certificates.TentacleListening;
        readonly string _endpointAThumbprint = Certificates.OctopusPublicThumbprint;
        readonly string _endpointBThumbprint = Certificates.TentacleListeningPublicThumbprint;
       
        [Test]
        public async Task ClientCanRespondToServerRequests()
        {
            await SetupBiDirectionalPollingClients(async (clientServiceA, clientServiceB) =>
            {
                var octopusResponse = await clientServiceA.SayHelloAsync("Hello");
                var tentacleResponse = await clientServiceB.SayHelloAsync("World");

                octopusResponse.Should().Be("Hello...");
                tentacleResponse.Should().Be("World...");
            });
        }

        [Test]
        public async Task ConcurrentOpposingCommunicationsDoNotBlock()
        {
            await SetupBiDirectionalPollingClients(async (clientServiceA, clientServiceB) =>
            {
                MyEchoService.SharedState = 0;
                var taskA = clientServiceA.BlockWaitingForSharedStateIncrementAsync().WithCancellation(CancellationToken);
                var taskB = clientServiceB.BlockWaitingForSharedStateIncrementAsync().WithCancellation(CancellationToken);

                await Task.WhenAll(taskA, taskB);

                taskA.Status.Should().Be(TaskStatus.RanToCompletion);
                taskB.Status.Should().Be(TaskStatus.RanToCompletion);
            });
        }

        [Test]
        public async Task ConcurrentClientAndServerRequestsCorrectlyInterleaved()
        {
            await SetupBiDirectionalPollingClients(async (clientServiceA, clientServiceB) =>
            {
                var taskA = RunMultipleEchoTask(clientServiceA).WithCancellation(CancellationToken);
                var taskB = RunMultipleEchoTask(clientServiceB).WithCancellation(CancellationToken);
                
                await Task.WhenAll(taskA, taskB);

                taskA.Status.Should().Be(TaskStatus.RanToCompletion);
                taskB.Status.Should().Be(TaskStatus.RanToCompletion);
            });
            
            async Task RunMultipleEchoTask(IAsyncClientMyEchoService tentacleEchoClient)
            {
                 var j = 0;
                 while (j++ < 10)
                 {
                     var tentacleResponse = await tentacleEchoClient.SayHelloAsync($"Hello{j}");
                     tentacleResponse.Should().Be($"Hello{j}...");
                 }
            }
        }

        async Task SetupBiDirectionalPollingClients(Func<IAsyncClientMyEchoService, IAsyncClientMyEchoService, Task> callback)
        {
            var _timeoutLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            var polling = new Uri($"poll://{DateTime.Now.Ticks.ToString()}");
            await using var halibutRuntimeA = CreateSimpleHalibutRuntime(_endpointACertificate);
            await using var halibutRuntimeB = CreateSimpleHalibutRuntime(_endpointBCertificate);
            
            halibutRuntimeA.Trust(_endpointBThumbprint);
            var port = halibutRuntimeA.Listen();
            
            halibutRuntimeB.Poll(polling, new ServiceEndPoint("https://localhost:" + port, _endpointAThumbprint, _timeoutLimits), CancellationToken.None);

            var clientServiceB = halibutRuntimeB.CreateAsyncClient<IMyEchoService, IAsyncClientMyEchoService>(new ServiceEndPoint(new Uri("https://localhost:" + port), _endpointAThumbprint, _timeoutLimits));
            var clientServiceA = halibutRuntimeA.CreateAsyncClient<IMyEchoService, IAsyncClientMyEchoService>(new ServiceEndPoint(polling, _endpointBThumbprint, _timeoutLimits));
            await callback(clientServiceA, clientServiceB);
        }
        
        [Test]
        public async Task ClientConnectingToListenerMustAlsoInitiatePollingToReceiveMessage()
        {
            var timeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            timeoutsAndLimits.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
            
            var polling = new Uri($"poll://{DateTime.Now.Ticks.ToString()}");
            await using var halibutRuntimeA = CreateSimpleHalibutRuntime(_endpointACertificate);
            await using var halibutRuntimeB = CreateSimpleHalibutRuntime(_endpointBCertificate);
            
            halibutRuntimeA.Trust(_endpointBThumbprint);
            var port = halibutRuntimeA.Listen();
            
            // Create client from EndpointA
            var clientServiceA = halibutRuntimeA.CreateAsyncClient<IMyEchoService, IAsyncClientMyEchoService>(new ServiceEndPoint(polling, _endpointBThumbprint, timeoutsAndLimits));
            
            // Since EndpointB is not polling, it will not receive the message after waiting some time
            Func<Task> taskA = () => clientServiceA.SayHelloAsync("Hello");
            await taskA.Should().ThrowAsync<HalibutClientException>();
            
            // Now start polling from EndpointB
            var listeningEndpoint = new ServiceEndPoint("https://localhost:" + port, _endpointAThumbprint, timeoutsAndLimits);
            halibutRuntimeB.Poll(polling, listeningEndpoint, CancellationToken);
            
            // ClientA can now send the message
            var result2 = await clientServiceA.SayHelloAsync("World").WithCancellation(CancellationToken);
            result2.Should().Be("World...");

            // Disconnect EndpointB, ClientA should no longer send message
            await halibutRuntimeB.DisconnectAsync(listeningEndpoint, CancellationToken);
            Func<Task> taskA2 = () => clientServiceA.SayHelloAsync("Hello");
            await taskA2.Should().ThrowAsync<HalibutClientException>();
            
            // EndpointB Reconnects so EndpointA can now be called
            halibutRuntimeB.Poll(polling, listeningEndpoint, CancellationToken);
            result2 = await clientServiceA.SayHelloAsync("World").WithCancellation(CancellationToken);
            result2.Should().Be("World...");
        }

        
        HalibutRuntime CreateSimpleHalibutRuntime(X509Certificate2 certificate2)
        {
            var services = new DelegateServiceFactory();
            services.Register<IMyEchoService, IAsyncMyEchoService>(() => new MyEchoService());
            return new HalibutRuntimeBuilder()
                .WithServerCertificate(certificate2)
                .WithServiceFactory(services)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();
        }

        public interface IMyEchoService
        {
            void BlockWaitingForSharedStateIncrement();
            string SayHello(string name);
        }

        public interface IAsyncClientMyEchoService
        {
            Task BlockWaitingForSharedStateIncrementAsync();
            Task<string> SayHelloAsync(string name);
        }

        public interface IAsyncMyEchoService
        {
            Task BlockWaitingForSharedStateIncrementAsync(CancellationToken cancellationToken);
            Task<string> SayHelloAsync(string name, CancellationToken cancellationToken);
        }

        public class MyEchoService : IAsyncMyEchoService
        {
            public static int SharedState = 0;
            public async Task BlockWaitingForSharedStateIncrementAsync(CancellationToken cancellationToken)
            {
                SharedState++;
                while (SharedState != 2)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            public async Task<string> SayHelloAsync(string name, CancellationToken cancellationToken)
            {
                await Task.CompletedTask;
                return name + "...";
            }
        }


    }
}
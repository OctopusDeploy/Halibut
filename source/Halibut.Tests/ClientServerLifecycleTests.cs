// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ClientServerLifecycleTests : BaseTest
    {
        [Test]
        public async Task ListeningConfiguration()
        {
            await using var server = RunServer(out var serverPort);

            await using var runtime = CreateRuntimeForListener();
            var client = CreateClient(runtime, serverPort);
            var result = await client.AddAsync(2, 2);
            result.Should().Be(4);
        }

        [Test]
        public async Task PollingConfiguration()
        {
            await using var server = RunServer(out var serverPort);
            await using var runtime = CreateRuntimeForPoller(server, out var client);
            var result = await client.AddAsync(2, 2);
            result.Should().Be(4);
        }

        [Test]
        public async Task ListeningThenPollingConfiguration()
        {
            // On NET4.8 with SslProtocols.None this will result in SSPI errors
            await ListeningConfiguration();
            await PollingConfiguration();
        }

        HalibutRuntime CreateRuntimeForListener()
        {
            var runtime = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.TentacleListening)
                .WithLogFactory(new TestLogFactory(HalibutLog))
                .Build();
            return runtime;
        }

        HalibutRuntime CreateRuntimeForPoller(HalibutRuntime serverRuntime, out IAsyncClientCalculatorService client)
        {
            var runtime = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.TentaclePolling)
                .WithLogFactory(new TestLogFactory(HalibutLog))
                .Build();
            var port = runtime.Listen();
            runtime.Trust(Certificates.OctopusPublicThumbprint);

            var pollEndpoint = new ServiceEndPoint(
                baseUri: new Uri($"https://localhost:{port}/"),
                remoteThumbprint: Certificates.TentaclePollingPublicThumbprint,
                halibutTimeoutsAndLimits: runtime.TimeoutsAndLimits
            )
            {
                TcpClientConnectTimeout = TimeSpan.FromSeconds(5)
            };
            var pollingUri = new Uri("poll://TEST-POLL");
            serverRuntime.Poll(pollingUri, pollEndpoint, CancellationToken);
            var clientEndpoint = new ServiceEndPoint(
                baseUri: pollingUri,
                remoteThumbprint: Certificates.OctopusPublicThumbprint,
                halibutTimeoutsAndLimits: runtime.TimeoutsAndLimits
            );
            client = runtime.CreateAsyncClient<ICalculatorService, IAsyncClientCalculatorService>(clientEndpoint);
            
            return runtime;
        }

        static IAsyncClientCalculatorService CreateClient(HalibutRuntime runtime, int port)
        {
            var endpoint = new ServiceEndPoint(
                baseUri: $"https://localhost:{port}",
                remoteThumbprint: Certificates.OctopusPublicThumbprint,
                halibutTimeoutsAndLimits: runtime.TimeoutsAndLimits
            );
            var client = runtime
                .CreateAsyncClient<ICalculatorService, IAsyncClientCalculatorService>(endpoint);
            return client;
        }

        static IServiceFactory CreateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<ICalculatorService, IAsyncCalculatorService>(() => new AsyncCalculatorService());
            return services;
        }

        HalibutRuntime RunServer(out int port)
        {
            var services = CreateServiceFactory();

            var runtime = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Octopus)
                .WithServiceFactory(services)
                .WithLogFactory(new TestLogFactory(HalibutLog))
                .Build();

            runtime.Trust(Certificates.TentacleListeningPublicThumbprint);
            runtime.Trust(Certificates.TentaclePollingPublicThumbprint);
            port = runtime.Listen();

            return runtime;
        }

        public class TestLogFactory : ILogFactory
        {
            readonly ILog _log;
            public TestLogFactory(ILog log)
            {
                _log = log;
            }

            public ILog ForEndpoint(Uri endpoint) => _log;

            public ILog ForPrefix(string endPoint) => _log;
        }
        
        public interface ICalculatorService
        {
            long Add(long a, long b);
        }
    
        public interface IAsyncCalculatorService
        {
            Task<long> AddAsync(long a, long b, CancellationToken cancellationToken);
        }

        public interface IAsyncClientCalculatorService
        {
            Task<long> AddAsync(long a, long b);
        }

        public class AsyncCalculatorService : IAsyncCalculatorService
        {
            public async Task<long> AddAsync(long a, long b, CancellationToken cancellationToken)
            {
                await Task.CompletedTask;
                return a + b;
            }
        }
    }
}
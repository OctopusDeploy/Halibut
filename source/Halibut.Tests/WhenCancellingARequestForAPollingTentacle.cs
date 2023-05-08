using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCancellingARequestForAPollingTentacle
    {
        public class AndTheRequestIsStillQueued
        {
            [Test]
            public async Task TheRequestShouldBeCancelled()
            {
                var cancellationTokenSource = new CancellationTokenSource();

                // No Tentacle
                using (var server = SetupServer())
                {
                    server.Listen();
                    var doSomeActionService = server.CreateClient<IDoSomeActionService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint, cancellationTokenSource.Token);

                    var waitForActionToBeCalled = new SemaphoreSlim(0, 1);
                    var task = Task.Run(() =>
                        {
                            waitForActionToBeCalled.Release(1);
                            doSomeActionService.Action();
                        },
                        CancellationToken.None);

                    // Try and ensure the request has been queued in Halibut
                    await waitForActionToBeCalled.WaitAsync(CancellationToken.None);
                    await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

                    cancellationTokenSource.Cancel();

                    Exception actualException = null;

                    try
                    {
                        await task;
                    }
                    catch (Exception ex)
                    {
                        actualException = ex;
                    }

                    actualException.Should().NotBeNull();
                    actualException.Should().BeOfType<OperationCanceledException>();
                }
            }
        }

        public class AndTheRequestHasBeenDequeuedButNoResponseReceived
        {
            [Test]
            public async Task TheRequestShouldNotBeCancelled()
            {
                var waitForActionToBeCalled = new SemaphoreSlim(0, 1);
                var calls = new List<DateTime>();
                var cancellationTokenSource = new CancellationTokenSource();

                var (server, tentacle, doSomeActionService) = SetupPollingServerAndTentacle(() =>
                {
                    calls.Add(DateTime.UtcNow);
                    waitForActionToBeCalled.Release(1);
                }, cancellationTokenSource.Token);

                using (server)
                using (tentacle)
                {
                    var task = Task.Run(() =>
                        {
                            doSomeActionService.Action();
                        },
                        CancellationToken.None);

                    await waitForActionToBeCalled.WaitAsync(CancellationToken.None);
                    cancellationTokenSource.Cancel();
                    await task;
                }

                calls.Should().HaveCount(1);
            }
        }

        static (HalibutRuntime server,
            IHalibutRuntime tentacle,
            IDoSomeActionService doSomeActionService)
            SetupPollingServerAndTentacle(Action doSomeActionServiceAction, CancellationToken cancellationToken)
        {
            var server = SetupServer();

            var serverPort = server.Listen();

            var serverUri = new Uri("https://localhost:" + serverPort);
            var tentacle = SetupPollingTentacle(serverUri, "poll://SQ-TENTAPOLL", doSomeActionServiceAction);

            var doSomeActionService = server.CreateClient<IDoSomeActionService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint, cancellationToken);

            return (server, tentacle, doSomeActionService);
        }

        static HalibutRuntime SetupPollingTentacle(Uri url, string pollingEndpoint, Action doSomeServiceAction)
        {
            var services = new DelegateServiceFactory();
            var doSomeActionService = new DoSomeActionService(doSomeServiceAction);

            services.Register<IDoSomeActionService>(() => doSomeActionService);
            services.Register<IEchoService>(() => new EchoService());

            var pollingTentacle = new HalibutRuntimeBuilder()
                .WithServiceFactory(services)
                .WithLogFactory(new TestContextLogFactory("Tentacle"))
                .WithServerCertificate(Certificates.TentaclePolling)
                .Build();

            pollingTentacle.Poll(new Uri(pollingEndpoint), new ServiceEndPoint(url, Certificates.OctopusPublicThumbprint));

            return pollingTentacle;
        }

        static HalibutRuntime SetupServer()
        {
            var octopus = new HalibutRuntimeBuilder()
                .WithLogFactory(new TestContextLogFactory("Server"))
                .WithServerCertificate(Certificates.Octopus)
                .Build();

            octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

            return octopus;
        }
    }
}
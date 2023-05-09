using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCancellingARequestForAPollingTentacle
    {
        public class AndTheRequestIsStillQueued
        {
            [Test]
            public void TheRequestShouldBeCancelled()
            {
                var cancellationTokenSource = new CancellationTokenSource();

                // No Tentacle
                // CancelWhenRequestQueuedPendingRequestQueueFactory cancels the cancellation token source when a request is queued
                using (var server = SetupServer(logFactory => new CancelWhenRequestQueuedPendingRequestQueueFactory(logFactory, cancellationTokenSource)))
                {
                    server.Listen();
                    var doSomeActionService = server.CreateClient<IDoSomeActionService>(
                        "poll://SQ-TENTAPOLL",
                        Certificates.TentaclePollingPublicThumbprint,
                        cancellationTokenSource.Token);

                    Exception actualException = null;

                    try
                    {
                        doSomeActionService.Action();
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
            public void TheRequestShouldNotBeCancelled()
            {
                var calls = new List<DateTime>();
                var cancellationTokenSource = new CancellationTokenSource();

                // CancelWhenRequestDequeuedPendingRequestQueueFactory cancels the cancellation token source when a request is dequeued
                var (server, tentacle, doSomeActionService) = SetupPollingServerAndTentacle(
                    logFactory => new CancelWhenRequestDequeuedPendingRequestQueueFactory(logFactory, cancellationTokenSource),
                    doSomeActionServiceAction: () =>
                    {
                        calls.Add(DateTime.UtcNow);

                        while (!cancellationTokenSource.IsCancellationRequested)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(10));
                        }

                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    },
                    cancellationTokenSource.Token);

                using (server)
                using (tentacle)
                {
                    doSomeActionService.Action();
                }

                calls.Should().HaveCount(1);
            }
        }

        static (HalibutRuntime server, IHalibutRuntime tentacle, IDoSomeActionService doSomeActionService) SetupPollingServerAndTentacle(
                Func<ILogFactory, IPendingRequestQueueFactory> factory,
                Action doSomeActionServiceAction,
                CancellationToken cancellationToken)
        {
            var server = SetupServer(factory);

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

        static HalibutRuntime SetupServer(Func<ILogFactory, IPendingRequestQueueFactory> factory = null)
        {
            var logFactory = new TestContextLogFactory("Server");

            var builder = new HalibutRuntimeBuilder()
                .WithLogFactory(logFactory)
                .WithServerCertificate(Certificates.Octopus);

            if (factory != null)
            {
                builder = builder.WithPendingRequestQueueFactory(factory(logFactory));
            }

            var server = builder.Build();

            server.Trust(Certificates.TentaclePollingPublicThumbprint);

            return server;
        }

        internal class CancelWhenRequestQueuedPendingRequestQueueFactory : IPendingRequestQueueFactory
        {
            readonly CancellationTokenSource cancellationTokenSource;
            readonly DefaultPendingRequestQueueFactory inner;

            public CancelWhenRequestQueuedPendingRequestQueueFactory(ILogFactory logFactory, CancellationTokenSource cancellationTokenSource)
            {
                this.cancellationTokenSource = cancellationTokenSource;
                this.inner = new DefaultPendingRequestQueueFactory(logFactory);
            }

            public IPendingRequestQueue CreateQueue(Uri endpoint)
            {
                return new Decorator(inner.CreateQueue(endpoint), cancellationTokenSource);
            }

            class Decorator : IPendingRequestQueue
            {
                readonly CancellationTokenSource cancellationTokenSource;
                readonly IPendingRequestQueue inner;

                public Decorator(IPendingRequestQueue inner, CancellationTokenSource cancellationTokenSource)
                {
                    this.inner = inner;
                    this.cancellationTokenSource = cancellationTokenSource;
                }

                public bool IsEmpty => inner.IsEmpty;
                public void ApplyResponse(ResponseMessage response, ServiceEndPoint destination) => inner.ApplyResponse(response, destination);
                public RequestMessage Dequeue() => inner.Dequeue();
                public async Task<RequestMessage> DequeueAsync() => await inner.DequeueAsync();

                public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
                {
                    var task = Task.Run(async () =>
                        {
                            while (inner.IsEmpty)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                            }

                            cancellationTokenSource.Cancel();
                        },
                        CancellationToken.None);

                    var result = await inner.QueueAndWaitAsync(request, cancellationToken);
                    await task;
                    return result;
                }
            }
        }

        internal class CancelWhenRequestDequeuedPendingRequestQueueFactory : IPendingRequestQueueFactory
        {
            readonly CancellationTokenSource cancellationTokenSource;
            readonly DefaultPendingRequestQueueFactory inner;

            public CancelWhenRequestDequeuedPendingRequestQueueFactory(ILogFactory logFactory, CancellationTokenSource cancellationTokenSource)
            {
                this.cancellationTokenSource = cancellationTokenSource;
                this.inner = new DefaultPendingRequestQueueFactory(logFactory);
            }

            public IPendingRequestQueue CreateQueue(Uri endpoint)
            {
                return new Decorator(inner.CreateQueue(endpoint), cancellationTokenSource);
            }

            class Decorator : IPendingRequestQueue
            {
                readonly CancellationTokenSource cancellationTokenSource;
                readonly IPendingRequestQueue inner;

                public Decorator(IPendingRequestQueue inner, CancellationTokenSource cancellationTokenSource)
                {
                    this.inner = inner;
                    this.cancellationTokenSource = cancellationTokenSource;
                }

                public bool IsEmpty => inner.IsEmpty;
                public void ApplyResponse(ResponseMessage response, ServiceEndPoint destination) => inner.ApplyResponse(response, destination);

                public RequestMessage Dequeue()
                {
                    var response = inner.Dequeue();
                    cancellationTokenSource.Cancel();
                    return response;
                }

                public async Task<RequestMessage> DequeueAsync()
                {
                    var response = await inner.DequeueAsync();
                    cancellationTokenSource.Cancel();
                    return response;
                }

                public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
                    => await inner.QueueAndWaitAsync(request, cancellationToken);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCancellingARequestForAPollingTentacle
    {
        public class AndTheRequestIsStillQueued : BaseTest
        {
            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
            public async Task TheRequestShouldBeCancelled(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var cancellationTokenSource = new CancellationTokenSource();
                
                using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .As<LatestClientAndLatestServiceBuilder>()
                           // No Tentacle
                           .NoService()
                           // CancelWhenRequestQueuedPendingRequestQueueFactory cancels the cancellation token source when a request is queued
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestQueuedPendingRequestQueueFactory(inner, cancellationTokenSource)))
                           .Build(CancellationToken))
                {
                    var doSomeActionService = clientAndService.CreateClient<IDoSomeActionService>(cancellationTokenSource.Token);

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

        public class AndTheRequestHasBeenDequeuedButNoResponseReceived : BaseTest
        {
            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
            public async Task TheRequestShouldNotBeCancelled(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var calls = new List<DateTime>();
                var cancellationTokenSource = new CancellationTokenSource();

                using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .As<LatestClientAndLatestServiceBuilder>()
                           .WithDoSomeActionService(() =>
                           {
                               calls.Add(DateTime.UtcNow);

                               while (!cancellationTokenSource.IsCancellationRequested)
                               {
                                   Thread.Sleep(TimeSpan.FromMilliseconds(10));
                               }

                               Thread.Sleep(TimeSpan.FromSeconds(1));
                           })
                           // CancelWhenRequestDequeuedPendingRequestQueueFactory cancels the cancellation token source when a request is queued
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestDequeuedPendingRequestQueueFactory(inner, cancellationTokenSource)))
                           .Build(CancellationToken))
                {
                    clientAndService.CreateClient<IDoSomeActionService>(cancellationTokenSource.Token).Action();
                }

                calls.Should().HaveCount(1);
            }
        }

        internal class CancelWhenRequestQueuedPendingRequestQueueFactory : IPendingRequestQueueFactory
        {
            readonly CancellationTokenSource cancellationTokenSource;
            readonly IPendingRequestQueueFactory inner;

            public CancelWhenRequestQueuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource cancellationTokenSource)
            {
                this.cancellationTokenSource = cancellationTokenSource;
                this.inner = inner;
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
                public int Count => inner.Count;
                public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination) => await inner.ApplyResponse(response, destination);
                public async Task<RequestMessage> DequeueAsync(CancellationToken cancellationToken) => await inner.DequeueAsync(cancellationToken);

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
            readonly IPendingRequestQueueFactory inner;

            public CancelWhenRequestDequeuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource cancellationTokenSource)
            {
                this.cancellationTokenSource = cancellationTokenSource;
                this.inner = inner;
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
                public int Count => inner.Count;
                public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination) => await inner.ApplyResponse(response, destination);
                
                public async Task<RequestMessage> DequeueAsync(CancellationToken cancellationToken)
                {
                    var response = await inner.DequeueAsync(cancellationToken);
                    cancellationTokenSource.Cancel();
                    return response;
                }

                public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
                    => await inner.QueueAndWaitAsync(request, cancellationToken);
            }
        }
    }
}
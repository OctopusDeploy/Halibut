using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
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
            public async Task TheRequestShouldBeCancelled_WhenTheRequestCancellationTokenIsCancelled(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var (tokenSourcesToCancel, halibutProxyRequestOptions) = CreateTokenSourceAndHalibutProxyRequestOptions();

                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .NoService()
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestQueuedPendingRequestQueueFactory(inner, tokenSourcesToCancel)))
                           .Build(CancellationToken))
                {
                    var doSomeActionService = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

                    (await AssertAsync.Throws<Exception>(() => doSomeActionService.ActionAsync(halibutProxyRequestOptions)))
                        .And.Should().Match(x => x is ConnectingRequestCancelledException);
                }
            }
        }

        public class AndTheRequestHasBeenDequeuedButNoResponseReceived : BaseTest
        {
            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
            public async Task TheRequestShouldBeCancelled_WhenTheRequestCancellationTokenIsCancelled(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var responseMessages = new List<ResponseMessage>();
                var shouldCancelWhenRequestDequeued = false;
                var calls = new List<DateTime>();
                var (tokenSourceToCancel, halibutProxyRequestOptions) = CreateTokenSourceAndHalibutProxyRequestOptions();

                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .WithHalibutLoggingLevel(LogLevel.Trace)
                           .WithDoSomeActionService(() =>
                           {
                               calls.Add(DateTime.UtcNow);

                               while (!tokenSourceToCancel.IsCancellationRequested)
                               {
                                   // Wait until the request is cancelled
                                   Thread.Sleep(TimeSpan.FromMilliseconds(100));
                               }

                               Thread.Sleep(TimeSpan.FromSeconds(1));
                           })
                           .WithEchoService()
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => 
                               new CancelWhenRequestDequeuedPendingRequestQueueFactory(inner, tokenSourceToCancel, ShouldCancel, OnResponseApplied)))
                           .Build(CancellationToken))
                {

                    shouldCancelWhenRequestDequeued = true;
                    var doSomeActionService = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();
                    var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoServiceWithOptions>();
                    
                    (await AssertAsync.Throws<Exception>(() => doSomeActionService.ActionAsync(halibutProxyRequestOptions)))
                        .And.Should().Match(x => x is TransferringRequestCancelledException);

                    // Ensure we can send another message to the Service which will validate the Client had the request cancelled to the socket
                    shouldCancelWhenRequestDequeued = false;
                    var started = Stopwatch.StartNew();
                    await echoService.SayHelloAsync(".", new HalibutProxyRequestOptions(CancellationToken.None));
                    // This should return quickly
                    started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
                }

                calls.Should().HaveCount(1);

                // Wait for all responses have been received
                await Task.Delay(TimeSpan.FromSeconds(5));

                // Ensure we did not get a valid response back from the doSomeActionService and that the request was cancelled to the socket.
                responseMessages.Should().HaveCount(2);
                responseMessages.ElementAt(0).Id.Should().Contain("IDoSomeActionService::ActionAsync");
                responseMessages.ElementAt(0).Error.Should().NotBeNull();
                responseMessages.ElementAt(0).Error.Message.Should().Contain("The Request was cancelled while Transferring");
                responseMessages.ElementAt(1).Error.Should().BeNull();
                responseMessages.ElementAt(1).Id.Should().Contain("IEchoService::SayHelloAsync");

                bool ShouldCancel()
                {
                    return shouldCancelWhenRequestDequeued;
                }

                void OnResponseApplied(ResponseMessage response)
                {
                    responseMessages.Add(response);
                }
            }
        }

        static (CancellationTokenSource TokenSourceToCancel, HalibutProxyRequestOptions HalibutProxyRequestOptions) CreateTokenSourceAndHalibutProxyRequestOptions()
        {
            var requestCancellationTokenSource = new CancellationTokenSource();
            var halibutProxyRequestOptions = new HalibutProxyRequestOptions(requestCancellationTokenSource.Token);
                
            return (requestCancellationTokenSource, halibutProxyRequestOptions);
        }

        /// <summary>
        /// CancelWhenRequestQueuedPendingRequestQueueFactory cancels the cancellation token source when a request is queued
        /// </summary>
        class CancelWhenRequestQueuedPendingRequestQueueFactory : IPendingRequestQueueFactory
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
                public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination)
                {
                    await inner.ApplyResponse(response, destination);
                }

                public async Task<RequestMessageWithCancellationToken> DequeueAsync(CancellationToken cancellationToken) => await inner.DequeueAsync(cancellationToken);

                public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken requestCancellationToken)
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

                    var result = await inner.QueueAndWaitAsync(request, requestCancellationToken);
                    await task;
                    return result;
                }
            }
        }

        /// <summary>
        /// CancelWhenRequestDequeuedPendingRequestQueueFactory cancels the cancellation token source when a request is queued
        /// </summary>
        class CancelWhenRequestDequeuedPendingRequestQueueFactory : IPendingRequestQueueFactory
        {
            readonly CancellationTokenSource cancellationTokenSource;
            readonly Func<bool> shouldCancel;
            readonly Action<ResponseMessage> onResponseApplied;
            readonly IPendingRequestQueueFactory inner;

            public CancelWhenRequestDequeuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource cancellationTokenSource, Func<bool> shouldCancel, Action<ResponseMessage> onResponseApplied)
            {
                this.cancellationTokenSource = cancellationTokenSource;
                this.shouldCancel = shouldCancel;
                this.inner = inner;
                this.onResponseApplied = onResponseApplied;
            }

            public IPendingRequestQueue CreateQueue(Uri endpoint)
            {
                return new Decorator(inner.CreateQueue(endpoint), cancellationTokenSource, shouldCancel, onResponseApplied);
            }

            class Decorator : IPendingRequestQueue
            {
                readonly CancellationTokenSource cancellationTokenSource;
                readonly Func<bool> shouldCancel;
                readonly Action<ResponseMessage> onResponseApplied;
                readonly IPendingRequestQueue inner;

                public Decorator(IPendingRequestQueue inner, CancellationTokenSource cancellationTokenSource, Func<bool> shouldCancel, Action<ResponseMessage> onResponseApplied)
                {
                    this.inner = inner;
                    this.cancellationTokenSource = cancellationTokenSource;
                    this.shouldCancel = shouldCancel;
                    this.onResponseApplied = onResponseApplied;
                }

                public bool IsEmpty => inner.IsEmpty;
                public int Count => inner.Count;
                public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination)
                {
                    onResponseApplied(response);
                    await inner.ApplyResponse(response, destination);
                }

                public async Task<RequestMessageWithCancellationToken> DequeueAsync(CancellationToken cancellationToken)
                {
                    var response = await inner.DequeueAsync(cancellationToken);

                    if (shouldCancel())
                    {
                        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2));
                    }

                    return response;
                }

                public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken requestCancellationToken)
                    => await inner.QueueAndWaitAsync(request, requestCancellationToken);
            }
        }
    }
}
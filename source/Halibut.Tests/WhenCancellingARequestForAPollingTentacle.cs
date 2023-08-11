using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.TestServices.SyncClientWithOptions;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCancellingARequestForAPollingTentacle
    {
        public class AndTheRequestIsStillQueued : BaseTest
        {
            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testAsyncClients: false)]
            public async Task TheRequestShouldBeCancelled_WhenUsingCreateClientWithASingleCancellationToken(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var cancellationTokenSource = new CancellationTokenSource();
                
                using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .NoService()
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestQueuedPendingRequestQueueFactory(inner, cancellationTokenSource)))
                           .Build(CancellationToken))
                {
                    var doSomeActionService = clientAndService.CreateClient<IDoSomeActionService>(cancellationTokenSource.Token);

                    Assert.Throws<OperationCanceledException>(() => doSomeActionService.Action());
                }
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testAsyncClients: false)]
            public async Task TheRequestShouldBeCancelled_WhenTheConnectingOrInProgressCancellationTokenIsCancelled_OnSyncClients(
                ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var tokenSourceToCancel = new CancellationTokenSource();
                var halibutProxyRequestOptions = new HalibutProxyRequestOptions(tokenSourceToCancel.Token, null);

                using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .NoService()
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestQueuedPendingRequestQueueFactory(inner, tokenSourceToCancel)))
                           .Build(CancellationToken))
                {
                    var doSomeActionService = clientAndService.CreateClient<IDoSomeActionService, ISyncClientDoSomeActionServiceWithOptions>();

                    Assert.Throws<OperationCanceledException>(() => doSomeActionService.Action(halibutProxyRequestOptions));
                }
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testSyncClients: false, additionalParameters: new object[]{ true, false })]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testSyncClients: false, additionalParameters: new object[]{ false, true })]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testSyncClients: false, additionalParameters: new object[]{ true, true })]
            public async Task TheRequestShouldBeCancelled_WhenTheConnectingOrInProgressCancellationTokenIsCancelled_OnAsyncClients(
                ClientAndServiceTestCase clientAndServiceTestCase, 
                bool connectingCancellationTokenCancelled,
                bool inProgressCancellationTokenCancelled)
            {
                var (tokenSourcesToCancel, halibutProxyRequestOptions) = CreateTokenSourceAndHalibutProxyRequestOptions(connectingCancellationTokenCancelled, inProgressCancellationTokenCancelled);

                using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .NoService()
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestQueuedPendingRequestQueueFactory(inner, tokenSourcesToCancel)))
                           .Build(CancellationToken))
                {
                    var doSomeActionService = clientAndService.CreateClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

                    await AssertAsync.Throws<OperationCanceledException>(() => doSomeActionService.ActionAsync(halibutProxyRequestOptions));
                }
            }
        }

        public class AndTheRequestHasBeenDequeuedButNoResponseReceived : BaseTest
        {
            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testAsyncClients: false)]
            public async Task TheRequestShouldNotBeCancelled_WhenUsingCreateClientWithASingleCancellationToken(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var calls = new List<DateTime>();
                var tokenSourceToCancel = new CancellationTokenSource();

                using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .WithDoSomeActionService(() =>
                           {
                               calls.Add(DateTime.UtcNow);

                               while (!tokenSourceToCancel.IsCancellationRequested)
                               {
                                   Thread.Sleep(TimeSpan.FromMilliseconds(10));
                               }

                               Thread.Sleep(TimeSpan.FromSeconds(1));
                           })
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestDequeuedPendingRequestQueueFactory(inner, tokenSourceToCancel)))
                           .Build(CancellationToken))
                {
                    var doSomeActionService = clientAndService.CreateClient<IDoSomeActionService>(tokenSourceToCancel.Token);
                    doSomeActionService.Action();

                }

                calls.Should().HaveCount(1);
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testAsyncClients: false)]
            public async Task TheRequestShouldNotBeCancelled_WhenTheConnectingCancellationTokenIsCancelled_OnSyncClients(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var calls = new List<DateTime>();
                var tokenSourceToCancel = new CancellationTokenSource();
                var halibutProxyRequestOptions = new HalibutProxyRequestOptions(tokenSourceToCancel.Token, null);

                using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .WithDoSomeActionService(() =>
                           {
                               calls.Add(DateTime.UtcNow);

                               while (!tokenSourceToCancel.IsCancellationRequested)
                               {
                                   Thread.Sleep(TimeSpan.FromMilliseconds(10));
                               }

                               Thread.Sleep(TimeSpan.FromSeconds(1));
                           })
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestDequeuedPendingRequestQueueFactory(inner, tokenSourceToCancel)))
                           .Build(CancellationToken))
                {
                    var doSomeActionService = clientAndService.CreateClient<IDoSomeActionService, ISyncClientDoSomeActionServiceWithOptions>();

                    doSomeActionService.Action(halibutProxyRequestOptions);
                }

                calls.Should().HaveCount(1);
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testSyncClients: false)]
            public async Task TheRequestShouldNotBeCancelled_WhenTheConnectingCancellationTokenIsCancelled_OnAsyncClients(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var calls = new List<DateTime>();
                var (tokenSourcesToCancel, halibutProxyRequestOptions) = CreateTokenSourceAndHalibutProxyRequestOptions(
                    connectingCancellationTokenCancelled: true, 
                    inProgressCancellationTokenCancelled: false);

                using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .WithDoSomeActionService(() =>
                           {
                               calls.Add(DateTime.UtcNow);

                               while (!tokenSourcesToCancel.All(x => x.IsCancellationRequested))
                               {
                                   Thread.Sleep(TimeSpan.FromMilliseconds(10));
                               }

                               Thread.Sleep(TimeSpan.FromSeconds(1));
                           })
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestDequeuedPendingRequestQueueFactory(inner, tokenSourcesToCancel)))
                           .Build(CancellationToken))
                {
                    var doSomeActionService = clientAndService.CreateClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

                    await doSomeActionService.ActionAsync(halibutProxyRequestOptions);
                }

                calls.Should().HaveCount(1);
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testSyncClients: false, additionalParameters: new object[]{ false, true })]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testSyncClients: false, additionalParameters: new object[]{ true, true })]
            public async Task TheRequestShouldBeCancelled_WhenTheInProgressCancellationTokenIsCancelled_OnAsyncClients(
                ClientAndServiceTestCase clientAndServiceTestCase, 
                bool connectingCancellationTokenCancelled,
                bool inProgressCancellationTokenCancelled)
            {
                var calls = new List<DateTime>();
                var (tokenSourcesToCancel, halibutProxyRequestOptions) = CreateTokenSourceAndHalibutProxyRequestOptions(connectingCancellationTokenCancelled, inProgressCancellationTokenCancelled);

                using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .AsLatestClientAndLatestServiceBuilder()
                           .WithHalibutLoggingLevel(LogLevel.Trace)
                           .WithDoSomeActionService(() =>
                           {
                               calls.Add(DateTime.UtcNow);

                               while (!tokenSourcesToCancel.All(x => x.IsCancellationRequested))
                               {
                                   Thread.Sleep(TimeSpan.FromMilliseconds(10));
                               }

                               Thread.Sleep(TimeSpan.FromSeconds(1));
                           })
                           .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestDequeuedPendingRequestQueueFactory(inner, tokenSourcesToCancel)))
                           .Build(CancellationToken))
                {
                    var doSomeActionService = clientAndService.CreateClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

                    await AssertAsync.Throws<OperationCanceledException>(() => doSomeActionService.ActionAsync(halibutProxyRequestOptions));
                }

                calls.Should().HaveCount(1);
            }
        }

        static (CancellationTokenSource[] ToeknSourcesToCancel, HalibutProxyRequestOptions HalibutProxyRequestOptions) CreateTokenSourceAndHalibutProxyRequestOptions(
            bool connectingCancellationTokenCancelled, 
            bool inProgressCancellationTokenCancelled)
        {
            var connectingCancellationTokenSource = new CancellationTokenSource();
            var inProgressCancellationTokenSource = new CancellationTokenSource();

            CancellationTokenSource[] tokenSourcesToCancel;

            if (connectingCancellationTokenCancelled && inProgressCancellationTokenCancelled)
            {
                tokenSourcesToCancel = new [] { connectingCancellationTokenSource, inProgressCancellationTokenSource };
            }
            else if (connectingCancellationTokenCancelled)
            {
                tokenSourcesToCancel = new [] { connectingCancellationTokenSource };
            }
            else
            {
                tokenSourcesToCancel = new [] { inProgressCancellationTokenSource };
            }

            var halibutProxyRequestOptions = new HalibutProxyRequestOptions(connectingCancellationTokenSource.Token, inProgressCancellationTokenSource.Token);
                
            return (tokenSourcesToCancel, halibutProxyRequestOptions);
        }

        /// <summary>
        /// CancelWhenRequestQueuedPendingRequestQueueFactory cancels the cancellation token source when a request is queued
        /// </summary>
        class CancelWhenRequestQueuedPendingRequestQueueFactory : IPendingRequestQueueFactory
        {
            readonly CancellationTokenSource[] cancellationTokenSources;
            readonly IPendingRequestQueueFactory inner;

            public CancelWhenRequestQueuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource[] cancellationTokenSources)
            {
                this.cancellationTokenSources = cancellationTokenSources;
                this.inner = inner;
            }

            public CancelWhenRequestQueuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource cancellationTokenSource) : this(inner, new[]{ cancellationTokenSource }) {
            }

            public IPendingRequestQueue CreateQueue(Uri endpoint)
            {
                return new Decorator(inner.CreateQueue(endpoint), cancellationTokenSources);
            }

            class Decorator : IPendingRequestQueue
            {
                readonly CancellationTokenSource[] cancellationTokenSources;
                readonly IPendingRequestQueue inner;

                public Decorator(IPendingRequestQueue inner, CancellationTokenSource[] cancellationTokenSources)
                {
                    this.inner = inner;
                    this.cancellationTokenSources = cancellationTokenSources;
                }

                public bool IsEmpty => inner.IsEmpty;
                public int Count => inner.Count;
                public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination) => await inner.ApplyResponse(response, destination);
                public async Task<RequestMessage> DequeueAsync(CancellationToken cancellationToken) => await inner.DequeueAsync(cancellationToken);

                public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken queuedRequestCancellationToken)
                {
                    var task = Task.Run(async () =>
                        {
                            while (inner.IsEmpty)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                            }

                            Parallel.ForEach(cancellationTokenSources, cancellationTokenSource => cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2)));
                        },
                        CancellationToken.None);

                    var result = await inner.QueueAndWaitAsync(request, queuedRequestCancellationToken);
                    await task;
                    return result;
                }

                public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, RequestCancellationTokens requestCancellationTokens)
                {
                    var task = Task.Run(async () =>
                        {
                            while (inner.IsEmpty)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                            }

                            Parallel.ForEach(cancellationTokenSources, cancellationTokenSource => cancellationTokenSource.Cancel());
                        },
                        CancellationToken.None);

                    var result = await inner.QueueAndWaitAsync(request, requestCancellationTokens);
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
            readonly CancellationTokenSource[] cancellationTokenSources;
            readonly IPendingRequestQueueFactory inner;

            public CancelWhenRequestDequeuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource[] cancellationTokenSources)
            {
                this.cancellationTokenSources = cancellationTokenSources;
                this.inner = inner;
            }

            public CancelWhenRequestDequeuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource cancellationTokenSource): this(inner, new []{ cancellationTokenSource })
            {
            }

            public IPendingRequestQueue CreateQueue(Uri endpoint)
            {
                return new Decorator(inner.CreateQueue(endpoint), cancellationTokenSources);
            }

            class Decorator : IPendingRequestQueue
            {
                readonly CancellationTokenSource[] cancellationTokenSources;
                readonly IPendingRequestQueue inner;

                public Decorator(IPendingRequestQueue inner, CancellationTokenSource[] cancellationTokenSources)
                {
                    this.inner = inner;
                    this.cancellationTokenSources = cancellationTokenSources;
                }

                public bool IsEmpty => inner.IsEmpty;
                public int Count => inner.Count;
                public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination) => await inner.ApplyResponse(response, destination);
                
                public async Task<RequestMessage> DequeueAsync(CancellationToken cancellationToken)
                {
                    var response = await inner.DequeueAsync(cancellationToken);
                    
                    Parallel.ForEach(cancellationTokenSources, cancellationTokenSource => cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2)));

                    return response;
                }

                public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken queuedRequestCancellationToken)
                    => await inner.QueueAndWaitAsync(request, queuedRequestCancellationToken);

                public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, RequestCancellationTokens requestCancellationTokens)
                    => await inner.QueueAndWaitAsync(request, requestCancellationTokens);
            }
        }
    }
}
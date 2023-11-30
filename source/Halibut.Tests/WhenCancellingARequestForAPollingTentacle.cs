using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.PendingRequestQueueFactories;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCancellingARequestForAPollingTentacle
    {
        public class AndTheRequestIsStillQueued : BaseTest
        {
            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, additionalParameters: new object[] { true, false })]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, additionalParameters: new object[] { false, true })]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, additionalParameters: new object[] { true, true })]
            public async Task TheRequestShouldBeCancelled_WhenTheConnectingOrInProgressCancellationTokenIsCancelled_OnAsyncClients(
                ClientAndServiceTestCase clientAndServiceTestCase,
                bool connectingCancellationTokenCancelled,
                bool inProgressCancellationTokenCancelled)
            {
                var (tokenSourcesToCancel, halibutProxyRequestOptions) = CreateTokenSourceAndHalibutProxyRequestOptions(connectingCancellationTokenCancelled, inProgressCancellationTokenCancelled);

                await using (var client = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder()
                                 .AsLatestClientBuilder()
                                 .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestQueuedPendingRequestQueueFactory(inner, tokenSourcesToCancel)))
                                 .Build(CancellationToken))
                {
                    var doSomeActionService = client.CreateClientWithoutService<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

                    await AssertException.Throws<OperationCanceledException>(() => doSomeActionService.ActionAsync(halibutProxyRequestOptions));
                }
            }
        }

        public class AndTheRequestHasBeenDequeuedButNoResponseReceived : BaseTest
        {
            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
            public async Task TheRequestShouldNotBeCancelled_WhenTheConnectingCancellationTokenIsCancelled_OnAsyncClients(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var calls = new List<DateTime>();
                var (tokenSourcesToCancel, halibutProxyRequestOptions) = CreateTokenSourceAndHalibutProxyRequestOptions(
                    connectingCancellationTokenCancelled: true, 
                    inProgressCancellationTokenCancelled: false);

                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
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
                    var doSomeActionService = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

                    await doSomeActionService.ActionAsync(halibutProxyRequestOptions);
                }

                calls.Should().HaveCount(1);
            }

            [Test]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, additionalParameters: new object[]{ false, true })]
            [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, additionalParameters: new object[]{ true, true })]
            public async Task TheRequestShouldBeCancelled_WhenTheInProgressCancellationTokenIsCancelled_OnAsyncClients(
                ClientAndServiceTestCase clientAndServiceTestCase, 
                bool connectingCancellationTokenCancelled,
                bool inProgressCancellationTokenCancelled)
            {
                var calls = new List<DateTime>();
                var (tokenSourcesToCancel, halibutProxyRequestOptions) = CreateTokenSourceAndHalibutProxyRequestOptions(connectingCancellationTokenCancelled, inProgressCancellationTokenCancelled);

                await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
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
                    var doSomeActionService = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

                    await AssertException.Throws<OperationCanceledException>(() => doSomeActionService.ActionAsync(halibutProxyRequestOptions));
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
    }
}
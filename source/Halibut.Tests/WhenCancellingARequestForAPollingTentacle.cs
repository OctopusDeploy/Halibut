using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.PendingRequestQueueFactories;
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

                await using (var client = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder()
                                 .AsLatestClientBuilder()
                                 .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => new CancelWhenRequestQueuedPendingRequestQueueFactory(inner, tokenSourcesToCancel)))
                                 .Build(CancellationToken))
                {
                    var doSomeActionService = client.CreateClientWithoutService<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();

                    await AssertionExtensions.Should(() => doSomeActionService.ActionAsync(halibutProxyRequestOptions)).ThrowAsync<ConnectingRequestCancelledException>();
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
                               new CancelWhenRequestDequeuedPendingRequestQueueFactory(inner, tokenSourceToCancel, ShouldCancelOnDequeue, OnResponseApplied)))
                           .Build(CancellationToken))
                {

                    shouldCancelWhenRequestDequeued = true;
                    var doSomeActionService = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionServiceWithOptions>();
                    var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoServiceWithOptions>();
                    
                    await AssertionExtensions.Should(() => doSomeActionService.ActionAsync(halibutProxyRequestOptions)).ThrowAsync<TransferringRequestCancelledException>();
                    
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
                responseMessages.ElementAt(0).Error!.Message.Should().Contain("The Request was cancelled while Transferring");
                responseMessages.ElementAt(1).Error.Should().BeNull();
                responseMessages.ElementAt(1).Id.Should().Contain("IEchoService::SayHelloAsync");

                bool ShouldCancelOnDequeue()
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
    }
}
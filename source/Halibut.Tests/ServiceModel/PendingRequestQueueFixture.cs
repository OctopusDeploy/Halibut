using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.ServiceModel
{
    public class PendingRequestQueueFixture : BaseTest
    {
        [Test]
        [SyncAndAsync]
        public async Task QueueAndWait_WillContinueWaitingUntilResponseIsApplied(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithAsync(syncOrAsync).WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();
            
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, CancellationToken);
            await sut.DequeueAsync(CancellationToken);
            

            // Act
            await Task.Delay(1000, CancellationToken);
            queueAndWaitTask.IsCompleted.Should().BeFalse();

            await sut.ApplyResponse(expectedResponse, request.Destination);
            
            // Assert
            var response = await queueAndWaitTask;
            response.Should().Be(expectedResponse);
        }

        [Test]
        [SyncAndAsync]
        public async Task QueueAndWait_WillIgnoreUnrelatedApplyResponses_AndShouldContinueWaiting(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithAsync(syncOrAsync).WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();
            var unexpectedResponse = new ResponseMessageBuilder(Guid.NewGuid().ToString()).Build();

            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, CancellationToken);
            await sut.DequeueAsync(CancellationToken);


            // Act
            await Task.Delay(1000, CancellationToken);
            queueAndWaitTask.IsCompleted.Should().BeFalse();

            // Apply unrelated responses
            await sut.ApplyResponse(null, request.Destination);
            await sut.ApplyResponse(unexpectedResponse, request.Destination);

            await Task.Delay(1000, CancellationToken);
            queueAndWaitTask.IsCompleted.Should().BeFalse();

            await sut.ApplyResponse(expectedResponse, request.Destination);


            // Assert
            var response = await queueAndWaitTask;
            response.Should().Be(expectedResponse);
        }

        [Test]
        [SyncAndAsync]
        public async Task QueueAndWait_WhenPollingRequestQueueTimeoutIsReached_WillStopWaitingAndClearRequest(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithAsync(syncOrAsync).WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(1000)))
                .Build();
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, CancellationToken);
            var response = await queueAndWaitTask;

            // Assert
            // Although we sleep for 1 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));
            response.Id.Should().Be(request.Id);
            response.Error.Message.Should().Be("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time (00:00:01), so the request timed out.");

            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }

        [Test]
        [SyncAndAsync]
        public async Task QueueAndWait_WhenRequestIsDequeued_ButPollingRequestQueueTimeoutIsReached_AndPollingRequestMaximumMessageProcessingTimeoutIsReached_WillStopWaitingAndClearRequest(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
                .WithAsync(syncOrAsync)
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.Zero) // Remove delay, otherwise we wait the full 20 seconds for DequeueAsync at the end of the test
                .Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(1000)))
                .WithServiceEndpoint(seb => seb.WithPollingRequestMaximumMessageProcessingTimeout(TimeSpan.FromMilliseconds(1000)))
                .Build();

            // Act
            var stopwatch = Stopwatch.StartNew();
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, CancellationToken);
            await sut.DequeueAsync(CancellationToken);
            var response = await queueAndWaitTask;

            // Assert
            // Although we sleep for 2 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(1800));
            response.Id.Should().Be(request.Id);
            response.Error.Message.Should().Be("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time (00:00:01), so the request timed out.");

            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }
        
        [Test]
        [SyncAndAsync]
        public async Task QueueAndWait_WhenRequestIsDequeued_ButPollingRequestQueueTimeoutIsReached_ShouldWaitTillRequestRespondsAndClearRequest(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
                .WithAsync(syncOrAsync)
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.Zero) // Remove delay, otherwise we wait the full 20 seconds for DequeueAsync at the end of the test
                .Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(200)))
                .Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();

            // Act
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, CancellationToken);
            var dequeued = await sut.DequeueAsync(CancellationToken);

            await Task.Delay(1000, CancellationToken);

            await sut.ApplyResponse(expectedResponse, request.Destination);

            var response = await queueAndWaitTask;

            // Assert
            dequeued.Should().NotBeNull().And.Be(request);
            response.Should().Be(expectedResponse);

            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }

        [Test]
        [SyncAndAsync]
        public async Task QueueAndWait_AddingMultipleItemsToQueueInOrder_ShouldDequeueInOrder(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithAsync(syncOrAsync).WithEndpoint(endpoint).Build();

            var requestsInOrder = Enumerable.Range(0, 3)
                .Select(_ => new RequestMessageBuilder(endpoint).Build())
                .ToList();

            // Act
            var queueAndWaitTasksInOrder = new List<Task<ResponseMessage>>();
            foreach (var request in requestsInOrder)
            {
                var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, CancellationToken);
                queueAndWaitTasksInOrder.Add(queueAndWaitTask);
            }

            // Assert
            foreach (var expectedRequest in requestsInOrder)
            {
                var request = await sut.DequeueAsync(CancellationToken);
                request.Should().Be(expectedRequest);
            }

            await ApplyResponsesConcurrentlyAndEnsureAllQueueResponsesMatch(sut, requestsInOrder, queueAndWaitTasksInOrder);
        }

        [Test]
        [SyncAndAsync]
        public async Task QueueAndWait_AddingMultipleItemsToQueueConcurrently_AllRequestsShouldBeSuccessfullyQueued(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithAsync(syncOrAsync).WithEndpoint(endpoint).Build();

            var requestsInOrder = Enumerable.Range(0, 30)
                .Select(_ => new RequestMessageBuilder(endpoint).Build())
                .ToList();

            // Act
            var queueAndWaitTasksInOrder = requestsInOrder
                .Select(request => StartQueueAndWait(sut, request, CancellationToken))
                .ToList();

            await WaitForQueueCountToBecome(sut, requestsInOrder.Count);

            // Assert
            var requests = new List<RequestMessage>();
            for (int i = 0; i < requestsInOrder.Count; i++)
            {
                var request = await sut.DequeueAsync(CancellationToken);
                requests.Add(request);
            }
            requests.Should().BeEquivalentTo(requestsInOrder);

            await ApplyResponsesConcurrentlyAndEnsureAllQueueResponsesMatch(sut, requestsInOrder, queueAndWaitTasksInOrder);
        }

        [Test]
        [SyncAndAsync]
        public async Task QueueAndWait_CancellingAPendingRequestBeforeItIsDequeued_ShouldThrowExceptionAndClearRequest(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithAsync(syncOrAsync).WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();

            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            // Assert
            await AssertionExtensions.Should(() => queueAndWaitTask).ThrowAsync<OperationCanceledException>();

            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }

        [Test]
        [SyncAndAsync]
        public async Task QueueAndWait_CancellingAPendingRequestAfterItIsDequeued_ShouldWaitTillRequestRespondsAndClearRequest(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
                .WithAsync(syncOrAsync)
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.Zero) // Remove delay, otherwise we wait the full 20 seconds for DequeueAsync at the end of the test
                .Build();
            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();

            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, cancellationTokenSource.Token);

            var dequeued = await sut.DequeueAsync(CancellationToken);

            // Cancel, and give the queue time to start waiting for a response
            cancellationTokenSource.Cancel();
            await Task.Delay(1000, CancellationToken);

            await sut.ApplyResponse(expectedResponse, request.Destination);

            var response = await queueAndWaitTask;

            // Assert
            dequeued.Should().NotBeNull().And.Be(request);
            response.Should().Be(expectedResponse);

            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }

        [Test]
        [SyncAndAsync]
        public async Task QueueAndWait_CancellingAPendingRequestAfterItIsDequeued_AndPollingRequestMaximumMessageProcessingTimeoutIsReached_WillStopWaiting(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithAsync(syncOrAsync).WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestMaximumMessageProcessingTimeout(TimeSpan.FromMilliseconds(1000)))
                .Build();

            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var stopwatch = Stopwatch.StartNew();
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, cancellationTokenSource.Token);
            
            await sut.DequeueAsync(CancellationToken);
            cancellationTokenSource.Cancel();
            var response = await queueAndWaitTask;

            // Assert
            // Although we sleep for 1 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));
            response.Id.Should().Be(request.Id);
            response.Error.Message.Should().Be("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time (00:00:01), so the request timed out.");
        }

        [Test]
        public async Task DequeueAsync_WithNothingQueued_WillWaitPollingQueueWaitTimeout_ShouldReturnNull()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
                .WithAsync(SyncOrAsync.Async)
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.FromSeconds(1))
                .Build();
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var request = await sut.DequeueAsync(CancellationToken);

            //Assert
            // Although we sleep for 1 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));
            request.Should().BeNull();
        }

        [Test]
        public async Task DequeueAsync_WithNothingQueued_WillWaitPollingQueueWaitTimeout_AfterPreviousRequestWasQueuedAndDequeued()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
                .WithAsync(SyncOrAsync.Async) // Bug only fixed on async version
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.FromSeconds(1))
                .Build();

            // Queue, Dequeue, and respond to a previous request
            var previousRequest = new RequestMessageBuilder(endpoint).Build();
            var expectedPreviousResponse = ResponseMessageBuilder.FromRequest(previousRequest).Build();

            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, previousRequest, CancellationToken);
            await sut.DequeueAsync(CancellationToken);
            await sut.ApplyResponse(expectedPreviousResponse, previousRequest.Destination);
            await queueAndWaitTask;

            // Act
            var stopwatch = Stopwatch.StartNew();
            var dequeuedRequest = await sut.DequeueAsync(CancellationToken);
            
            // Assert
            // Although we sleep for 1 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));
            dequeuedRequest.Should().BeNull();
        }

        [Test]
        [SyncAndAsync]
        public async Task DequeueAsync_WillContinueWaitingUntilItemIsQueued(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithAsync(syncOrAsync).WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();

            // Act
            var stopwatch = Stopwatch.StartNew();
            var dequeueTask = sut.DequeueAsync(CancellationToken);

            await Task.Delay(1000, CancellationToken);

            var queueAndWaitTask = StartQueueAndWait(sut, request, CancellationToken);
            
            var dequeuedRequest = await dequeueTask;
            
            // Assert
            // Although we sleep for 1 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));

            dequeuedRequest.Should().Be(request);

            // Apply a response so we can prove this counts as taking a message.
            await sut.ApplyResponse(expectedResponse, request.Destination);
            var response = await queueAndWaitTask;
            response.Should().Be(expectedResponse);
        }
        
        [Test]
        [SyncAndAsync]
        public async Task DequeueAsync_WithMultipleDequeueCallsWaiting_WhenSingleRequestIsQueued_ThenOnlyOneCallersReceivesRequest(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithAsync(syncOrAsync).WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();
            
            var dequeueTasks = Enumerable.Range(0, 30)
                .Select(_ => sut.DequeueAsync(CancellationToken))
                .ToArray();

            await Task.Delay(1000, CancellationToken);

            // Act
            var queueAndWaitTask = StartQueueAndWait(sut, request, CancellationToken);

            // Assert
            await Task.WhenAll(dequeueTasks);

            await sut.ApplyResponse(expectedResponse, request.Destination);
            await queueAndWaitTask;

            var singleDequeuedRequest = dequeueTasks.Should().ContainSingle(t => t.Result != null).Subject.Result;
            singleDequeuedRequest.Should().Be(request);

        }

        [Test]
        [SyncAndAsync]
        public async Task ApplyResponse_AfterRequestHasBeenCollected_AndWaitingHasBeenCancelled_ResponseIsIgnored(SyncOrAsync syncOrAsync)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
                .WithAsync(syncOrAsync)
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.Zero) // Remove delay, otherwise we wait the full 20 seconds for DequeueAsync at the end of the test
                .Build();

            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();

            var cancellationTokenSource = new CancellationTokenSource();
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            // Allow cancellation to occur before we dequeue.
            await Task.Delay(1000, CancellationToken);
            await sut.DequeueAsync(CancellationToken);

            await AssertionExtensions.Should(() => queueAndWaitTask).ThrowAsync<OperationCanceledException>();


            // Act
            await sut.ApplyResponse(expectedResponse, request.Destination);


            // Assert
            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }

        async Task<Task<ResponseMessage>> StartQueueAndWaitAndWaitForRequestToBeQueued(
            IPendingRequestQueue pendingRequestQueue,
            RequestMessage request,
            CancellationToken queueAndWaitCancellationToken)
        {
            var count = pendingRequestQueue.Count;

            var task = StartQueueAndWait(pendingRequestQueue, request, queueAndWaitCancellationToken);

            await WaitForQueueCountToBecome(pendingRequestQueue, count + 1);

            return task;
        }

        Task<ResponseMessage> StartQueueAndWait(IPendingRequestQueue pendingRequestQueue, RequestMessage request, CancellationToken queueAndWaitCancellationToken)
        {
            var task = Task.Run(
                async () => await pendingRequestQueue.QueueAndWaitAsync(request, queueAndWaitCancellationToken),
                CancellationToken);
            return task;
        }

        async Task WaitForQueueCountToBecome(IPendingRequestQueue pendingRequestQueue, int count)
        {
            while (pendingRequestQueue.Count != count)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
            }
        }
        
        static async Task ApplyResponsesConcurrentlyAndEnsureAllQueueResponsesMatch(
            IPendingRequestQueue sut,
            List<RequestMessage> requestsInOrder,
            List<Task<ResponseMessage>> queueAndWaitTasksInOrder)
        {
            var expectedResponsesInOrder = requestsInOrder
                .Select(request => ResponseMessageBuilder.FromRequest(request).Build())
                .ToList();

            //Concurrently apply responses to prove this does not cause issues.
            var applyResponseTasks = requestsInOrder
                .Select((r,i) => Task.Factory.StartNew(async () => await sut.ApplyResponse(expectedResponsesInOrder[i], r.Destination)))
                .ToList();
            
            await Task.WhenAll(applyResponseTasks);

            var index = 0;
            foreach (var queueAndWaitTask in queueAndWaitTasksInOrder)
            {
                var response = await queueAndWaitTask;

                var expectedResponse = expectedResponsesInOrder[index++];
                response.Should().Be(expectedResponse);
            }
        }
    }
}
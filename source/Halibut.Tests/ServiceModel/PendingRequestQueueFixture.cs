using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Support.TestCases;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.ServiceModel
{
    public class PendingRequestQueueFixture
    {
        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task QueueAndWait_WillContinueWaitingUntilResponseIsApplied(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();
            
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(async, sut, request);
            await sut.DequeueAsync();
            

            // Act
            await Task.Delay(1000, CancellationToken.None);
            queueAndWaitTask.IsCompleted.Should().BeFalse();

            sut.ApplyResponse(expectedResponse, request.Destination);
            
            // Assert
            var response = await queueAndWaitTask;
            response.Should().Be(expectedResponse);
        }

        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task QueueAndWait_WillIgnoreUnrelatedApplyResponses_AndShouldContinueWaiting(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();
            var unexpectedResponse = new ResponseMessageBuilder(Guid.NewGuid().ToString()).Build();

            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(async, sut, request);
            await sut.DequeueAsync();


            // Act
            await Task.Delay(1000, CancellationToken.None);
            queueAndWaitTask.IsCompleted.Should().BeFalse();

            // Apply unrelated responses
            sut.ApplyResponse(null, request.Destination);
            sut.ApplyResponse(unexpectedResponse, request.Destination);

            await Task.Delay(1000, CancellationToken.None);
            queueAndWaitTask.IsCompleted.Should().BeFalse();

            sut.ApplyResponse(expectedResponse, request.Destination);


            // Assert
            var response = await queueAndWaitTask;
            response.Should().Be(expectedResponse);
        }

        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task QueueAndWait_WhenPollingRequestQueueTimeoutIsReached_WillStopWaitingAndClearRequest(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(1000)))
                .Build();
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(async, sut, request);
            var response = await queueAndWaitTask;

            // Assert
            // Although we sleep for 1 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));
            response.Id.Should().Be(request.Id);
            response.Error.Message.Should().Be("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time (00:00:01), so the request timed out.");

            var next = await sut.DequeueAsync();
            next.Should().BeNull();
        }

        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task QueueAndWait_WhenRequestIsDequeued_ButPollingRequestQueueTimeoutIsReached_AndPollingRequestMaximumMessageProcessingTimeoutIsReached_WillStopWaitingAndClearRequest(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(1000)))
                .WithServiceEndpoint(seb => seb.WithPollingRequestMaximumMessageProcessingTimeout(TimeSpan.FromMilliseconds(1000)))
                .Build();

            // Act
            var stopwatch = Stopwatch.StartNew();
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(async, sut, request);
            await sut.DequeueAsync();
            var response = await queueAndWaitTask;

            // Assert
            // Although we sleep for 2 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(1800));
            response.Id.Should().Be(request.Id);
            response.Error.Message.Should().Be("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time (00:00:01), so the request timed out.");

            var next = await sut.DequeueAsync();
            next.Should().BeNull();
        }
        
        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task QueueAndWait_WhenRequestIsDequeued_ButPollingRequestQueueTimeoutIsReached_ShouldWaitTillRequestRespondsAndClearRequest(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(200)))
                .Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();

            // Act
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(async, sut, request);
            var dequeued = await sut.DequeueAsync();

            await Task.Delay(1000, CancellationToken.None);

            sut.ApplyResponse(expectedResponse, request.Destination);

            var response = await queueAndWaitTask;

            // Assert
            dequeued.Should().NotBeNull().And.Be(request);
            response.Should().Be(expectedResponse);

            var next = await sut.DequeueAsync();
            next.Should().BeNull();
        }

        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task QueueAndWait_AddingMultipleItemsToQueueInOrder_ShouldDequeueInOrder(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();

            var requestsInOrder = Enumerable.Range(0, 3)
                .Select(_ => new RequestMessageBuilder(endpoint).Build())
                .ToList();

            // Act
            var queueAndWaitTasksInOrder = new List<Task<ResponseMessage>>();
            foreach (var request in requestsInOrder)
            {
                var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(async, sut, request);
                queueAndWaitTasksInOrder.Add(queueAndWaitTask);
            }

            // Assert
            foreach (var expectedRequest in requestsInOrder)
            {
                var request = await sut.DequeueAsync();
                request.Should().Be(expectedRequest);
            }

            await ApplyResponsesAndEnsureAllQueueResponsesShouldMatch(sut, requestsInOrder, queueAndWaitTasksInOrder);
        }

        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task QueueAndWait_AddingMultipleItemsToQueueConcurrently_AllRequestsShouldBeSuccessfullyQueued(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();

            var requestsInOrder = Enumerable.Range(0, 30)
                .Select(_ => new RequestMessageBuilder(endpoint).Build())
                .ToList();

            // Act
            var queueAndWaitTasksInOrder = requestsInOrder
                .Select(request => StartQueueAndWait(async, sut, request))
                .ToList();

            await WaitForQueueCountToBecome(sut, requestsInOrder.Count);

            // Assert
            for (int i = 0; i < requestsInOrder.Count; i++)
            {
                var request = await sut.DequeueAsync();
                requestsInOrder.Should().Contain(request);
            }

            await ApplyResponsesAndEnsureAllQueueResponsesShouldMatch(sut, requestsInOrder, queueAndWaitTasksInOrder);
        }

        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task QueueAndWait_CancellingAPendingRequestBeforeItIsDequeued_ShouldThrowExceptionAndClearRequest(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();

            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(async, sut, request, cancellationTokenSource);

            cancellationTokenSource.Cancel();

            // Assert
            await AssertionExtensions.Should(() => queueAndWaitTask).ThrowAsync<OperationCanceledException>();

            var next = await sut.DequeueAsync();
            next.Should().BeNull();
        }

        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task QueueAndWait_CancellingAPendingRequestAfterItIsDequeued_ShouldWaitTillRequestRespondsAndClearRequest(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();

            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(async, sut, request, cancellationTokenSource);

            var dequeued = await sut.DequeueAsync();

            // Cancel, and give the queue time to start waiting for a response
            cancellationTokenSource.Cancel();
            await Task.Delay(1000, CancellationToken.None);

            sut.ApplyResponse(expectedResponse, request.Destination);

            var response = await queueAndWaitTask;

            // Assert
            dequeued.Should().NotBeNull().And.Be(request);
            response.Should().Be(expectedResponse);

            var next = await sut.DequeueAsync();
            next.Should().BeNull();
        }

        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task QueueAndWait_CancellingAPendingRequestAfterItIsDequeued_AndPollingRequestMaximumMessageProcessingTimeoutIsReached_WillStopWaiting(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestMaximumMessageProcessingTimeout(TimeSpan.FromMilliseconds(1000)))
                .Build();

            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var stopwatch = Stopwatch.StartNew();
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(async, sut, request, cancellationTokenSource);
            
            await sut.DequeueAsync();
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
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.FromSeconds(1))
                .Build();
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var request = await sut.DequeueAsync();

            //Assert
            // Although we sleep for 1 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));
            request.Should().BeNull();
        }

        [Test]
        [TestCaseSource(typeof(AsyncTestCase))]
        public async Task DequeueAsync_WillContinueWaitingUntilItemIsQueued(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();

            // Act
            var stopwatch = Stopwatch.StartNew();
            var dequeueTask = sut.DequeueAsync();

            await Task.Delay(1000, CancellationToken.None);

            var queueAndWaitTask = StartQueueAndWait(async, sut, request);
            
            var dequeuedRequest = await dequeueTask;
            
            // Assert
            // Although we sleep for 1 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));

            dequeuedRequest.Should().Be(request);

            // Apply a response so we can prove this counts as taking a message.
            sut.ApplyResponse(expectedResponse, request.Destination);
            var response = await queueAndWaitTask;
            response.Should().Be(expectedResponse);
        }

        static async Task<Task<ResponseMessage>> StartQueueAndWaitAndWaitForRequestToBeQueued(
            bool async,
            PendingRequestQueue pendingRequestQueue,
            RequestMessage request,
            CancellationTokenSource? cancellationTokenSource = null)
        {
            var count = pendingRequestQueue.Count;

            var task = StartQueueAndWait(async, pendingRequestQueue, request, cancellationTokenSource);

            await WaitForQueueCountToBecome(pendingRequestQueue, count + 1);

            return task;
        }

        static async Task WaitForQueueCountToBecome(PendingRequestQueue pendingRequestQueue, int count)
        {
            while (pendingRequestQueue.Count != count)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken.None);
            }
        }

        static Task<ResponseMessage> StartQueueAndWait(
            bool async, 
            PendingRequestQueue pendingRequestQueue, 
            RequestMessage request, 
            CancellationTokenSource? cancellationTokenSource = null)
        {
            cancellationTokenSource ??= new CancellationTokenSource();

            var task = async
                ? Task.Run(
                    async () => await pendingRequestQueue.QueueAndWaitAsync(request, cancellationTokenSource.Token),
                    CancellationToken.None)
                : Task.Run(
                    () => pendingRequestQueue.QueueAndWait(request, cancellationTokenSource.Token),
                    CancellationToken.None);
            return task;
        }
        
        static async Task ApplyResponsesAndEnsureAllQueueResponsesShouldMatch(
            PendingRequestQueue sut,
            List<RequestMessage> requestsInOrder,
            List<Task<ResponseMessage>> queueAndWaitTasksInOrder)
        {
            var expectedResponsesInOrder = requestsInOrder
                .Select(request => ResponseMessageBuilder.FromRequest(request).Build())
                .ToList();

            //Concurrently apply responses to prove this does not cause issues.
            var applyResponseTasks = requestsInOrder
                .Select((r,i) => Task.Factory.StartNew(() => sut.ApplyResponse(expectedResponsesInOrder[i], r.Destination)))
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
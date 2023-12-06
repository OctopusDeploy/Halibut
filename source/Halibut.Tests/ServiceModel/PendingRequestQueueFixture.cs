using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Support;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.ServiceModel
{
    public class PendingRequestQueueFixture : BaseTest
    {
        [Test]
        public async Task QueueAndWait_WillContinueWaitingUntilResponseIsApplied()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
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
        public async Task QueueAndWait_WillIgnoreUnrelatedApplyResponses_AndShouldContinueWaiting()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();
            var unexpectedResponse = new ResponseMessageBuilder(Guid.NewGuid().ToString()).Build();

            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, CancellationToken);
            await sut.DequeueAsync(CancellationToken);


            // Act
            await Task.Delay(1000, CancellationToken);
            queueAndWaitTask.IsCompleted.Should().BeFalse();

            // Apply unrelated responses
            await sut.ApplyResponse(null!, request.Destination);
            await sut.ApplyResponse(unexpectedResponse, request.Destination);

            await Task.Delay(1000, CancellationToken);
            queueAndWaitTask.IsCompleted.Should().BeFalse();

            await sut.ApplyResponse(expectedResponse, request.Destination);


            // Assert
            var response = await queueAndWaitTask;
            response.Should().Be(expectedResponse);
        }

        [Test]
        public async Task QueueAndWait_WhenPollingRequestQueueTimeoutIsReached_WillStopWaitingAndClearRequest()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
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
            response.Error!.Message.Should().Be("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time (00:00:01), so the request timed out.");

            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }

        [Test]
        public async Task QueueAndWait_WhenRequestIsDequeued_ButPollingRequestQueueTimeoutIsReached_AndPollingRequestMaximumMessageProcessingTimeoutIsReached_WillStopWaitingAndClearRequest()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.Zero) // Remove delay, otherwise we wait the full 20 seconds for DequeueAsync at the end of the test
                .WithRelyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout(false)
                .Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(1000)))
                .WithServiceEndpoint(seb => seb.WithPollingRequestMaximumMessageProcessingTimeout(TimeSpan.FromMilliseconds(1000)))
                .Build();

            // Act
            var stopwatch = Stopwatch.StartNew();
            var (queueAndWaitTask, _) = await QueueAndDequeueRequest_ForTimeoutTestingOnly_ToCopeWithRaceCondition(sut, request, CancellationToken);
            var response = await queueAndWaitTask;

            // Assert
            // Although we sleep for 2 second, sometimes it can be just under. So be generous with the buffer.
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(1800));
            response.Id.Should().Be(request.Id);
            response.Error!.Message.Should().Be("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time (00:00:01), so the request timed out.");

            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }
        
        [Test]
        public async Task QueueAndWait_WhenRequestIsDequeued_WithRelyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout_WillWaitForeverUntilResponseIsSet()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.Zero) // Remove delay, otherwise we wait the full 20 seconds for DequeueAsync at the end of the test
                .WithRelyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout(true)
                .Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(1000)))
                //Make PollingRequestMaximumMessageProcessingTimeout super low to prove we are not respecting it
                .WithServiceEndpoint(seb => seb.WithPollingRequestMaximumMessageProcessingTimeout(TimeSpan.FromMilliseconds(1)))
                .Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();

            // Act
            var (queueAndWaitTask, dequeued) = await QueueAndDequeueRequest_ForTimeoutTestingOnly_ToCopeWithRaceCondition(sut, request, CancellationToken);
            
            var delayTask = Task.Delay(TimeSpan.FromSeconds(10));
            var finishedTask = await Task.WhenAny(queueAndWaitTask, delayTask);

            finishedTask.Should().Be(delayTask);
            
            await sut.ApplyResponse(expectedResponse, request.Destination);

            var response = await queueAndWaitTask;

            // Assert
            dequeued.Should().NotBeNull("We should have removed the item from the queue before it timed out.").And.Be(request);
            response.Should().Be(expectedResponse);

            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }

        [Test]
        public async Task QueueAndWait_WhenRequestIsDequeued_ButPollingRequestQueueTimeoutIsReached_ShouldWaitTillRequestRespondsAndClearRequest()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.Zero) // Remove delay, otherwise we wait the full 20 seconds for DequeueAsync at the end of the test
                .Build();
            var request = new RequestMessageBuilder(endpoint)
                .WithServiceEndpoint(seb => seb.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(1000)))
                .Build();
            var expectedResponse = ResponseMessageBuilder.FromRequest(request).Build();

            // Act
            var (queueAndWaitTask, dequeued) = await QueueAndDequeueRequest_ForTimeoutTestingOnly_ToCopeWithRaceCondition(sut, request, CancellationToken);

            await Task.Delay(2000, CancellationToken);

            await sut.ApplyResponse(expectedResponse, request.Destination);

            var response = await queueAndWaitTask;

            // Assert
            dequeued.Should().NotBeNull("We should have removed the item from the queue before it timed out.").And.Be(request);
            response.Should().Be(expectedResponse);

            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }
        
        [Test]
        public async Task QueueAndWait_AddingMultipleItemsToQueueInOrder_ShouldDequeueInOrder()
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
        public async Task QueueAndWait_AddingMultipleItemsToQueueConcurrently_AllRequestsShouldBeSuccessfullyQueued()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();

            var requestsInOrder = Enumerable.Range(0, 30)
                .Select(_ => new RequestMessageBuilder(endpoint).Build())
                .ToList();

            // Act
            var queueAndWaitTasksInOrder = requestsInOrder
                .Select(request => StartQueueAndWait(sut, request, CancellationToken))
                .ToList();

            await WaitForQueueCountToBecome(sut, requestsInOrder.Count);

            // Assert
            var requests = new List<RequestMessage?>();
            for (int i = 0; i < requestsInOrder.Count; i++)
            {
                var request = await sut.DequeueAsync(CancellationToken);
                requests.Add(request);
            }
            requests.Should().BeEquivalentTo(requestsInOrder);

            await ApplyResponsesConcurrentlyAndEnsureAllQueueResponsesMatch(sut, requestsInOrder, queueAndWaitTasksInOrder);
        }

        [Test]
        [NonParallelizable]
        public async Task QueueAndWait_Can_Queue_Dequeue_Apply_VeryLargeNumberOfRequests()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();

            // Choose a number large enough that it will fail when doing the 'synchronous' version.
            var requestsInOrder = Enumerable.Range(0, 5000)
                .Select(_ => new RequestMessageBuilder(endpoint).Build())
                .ToList();

            // Act
            var queueAndWaitTasksInOrder = requestsInOrder
                .Select(request => StartQueueAndWait(sut, request, CancellationToken))
                .ToList();

            await WaitForQueueCountToBecome(sut, requestsInOrder.Count);

            var dequeueTasks = requestsInOrder
                .Select(_ => sut.DequeueAsync(CancellationToken))
                .ToList();

            await Task.WhenAll(dequeueTasks);
            
            // Assert
            await ApplyResponsesConcurrentlyAndEnsureAllQueueResponsesMatch(sut, requestsInOrder, queueAndWaitTasksInOrder);
        }

        [Test]
        [NonParallelizable]
        public async Task QueueAndWait_Can_Queue_Dequeue_WhenRequestsAreBeingCancelled()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";
            const int totalRequest = 500;
            const int minimumCancelledRequest = 100;

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            
            var requestsInOrder = Enumerable.Range(0, totalRequest)
                .Select(_ => new RequestMessageBuilder(endpoint).Build())
                .ToList();

            // Act
            var queueAndWaitTasksInOrder = requestsInOrder
                .Select(request =>
                {
                    var requestCancellationTokenSource = new CancellationTokenSource();
                    return new Tuple<Task<ResponseMessage>, CancellationTokenSource>(
                        StartQueueAndWait(sut, request, requestCancellationTokenSource.Token), 
                        requestCancellationTokenSource);
                })
                .ToList();

            await WaitForQueueCountToBecome(sut, requestsInOrder.Count);
            
            var index = 0;
            var cancelled = 0;
            var dequeueTasks = new ConcurrentBag<Task<RequestMessage?>>();

            var cancelSomeTask = Task.Run(() =>
            {
                foreach (var _ in requestsInOrder)
                {
                    var currentIndex = Interlocked.Increment(ref index);

                    if(currentIndex % 2 == 0)
                    {
                        Interlocked.Increment(ref cancelled);
                        queueAndWaitTasksInOrder.ElementAt(index-1).Item2.Cancel();
                    }
                }
            });

            // Cancellation is slow so give it a head start
            while (cancelled < minimumCancelledRequest)
            {
                await Task.Delay(10);
            }

            var dequeueAllTask = Task.Run(() =>
            {
                foreach (var _ in requestsInOrder)
                {
                    var task = sut.DequeueAsync(CancellationToken);
                    dequeueTasks.Add(task);
                }
            });

            await Task.WhenAll(cancelSomeTask, dequeueAllTask);

            var completedTask = await Task.WhenAll(dequeueTasks);

            foreach (var queueAndWait in queueAndWaitTasksInOrder)
            {
                queueAndWait.Item2.Dispose();
            }

            // Assert
            // This test is pretty non-deterministic. It attempts to create some cancellation chaos
            // and we just want to ensure some things did cancel and some things did complete and nothing
            // threw an exception
            completedTask.Count(x => x != null).Should().BeLessThan(totalRequest - minimumCancelledRequest).And.BeGreaterThanOrEqualTo(totalRequest / 2);
        }

        [Test]
        public async Task QueueAndWait_CancellingAPendingRequestBeforeItIsDequeued_ShouldThrowExceptionAndClearRequest()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
            var request = new RequestMessageBuilder(endpoint).Build();

            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            // Assert
            await AssertException.Throws<OperationCanceledException>(queueAndWaitTask);

            var next = await sut.DequeueAsync(CancellationToken);
            next.Should().BeNull();
        }

        [Test]
        public async Task QueueAndWait_CancellingAPendingRequestAfterItIsDequeued_ShouldWaitTillRequestRespondsAndClearRequest()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
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
        public async Task QueueAndWait_CancellingAPendingRequestAfterItIsDequeued_AndPollingRequestMaximumMessageProcessingTimeoutIsReached_WillStopWaiting()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
                .WithEndpoint(endpoint)
                .WithRelyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout(false)
                .Build();
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
            response.Error!.Message.Should().Be("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time (00:00:01), so the request timed out.");
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
                .WithEndpoint(endpoint)
                .WithPollingQueueWaitTimeout(TimeSpan.FromSeconds(1))
                .Build();

            // Queue, Dequeue, and respond to a previous request
            var previousRequest = new RequestMessageBuilder(endpoint).Build();
            var expectedPreviousResponse = ResponseMessageBuilder.FromRequest(previousRequest).Build();

            var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, previousRequest ,CancellationToken);
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
        public async Task DequeueAsync_WillContinueWaitingUntilItemIsQueued()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
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
        public async Task DequeueAsync_WithMultipleDequeueCallsWaiting_WhenSingleRequestIsQueued_ThenOnlyOneCallersReceivesRequest()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder().WithEndpoint(endpoint).Build();
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

            var singleDequeuedRequest = await dequeueTasks.Should().ContainSingle(t => t.Result != null).Subject;
            singleDequeuedRequest.Should().Be(request);
        }

        [Test]
        public async Task ApplyResponse_AfterRequestHasBeenCollected_AndWaitingHasBeenCancelled_ResponseIsIgnored()
        {
            // Arrange
            const string endpoint = "poll://endpoint001";

            var sut = new PendingRequestQueueBuilder()
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

            await AssertException.Throws<OperationCanceledException>(queueAndWaitTask);


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

        async Task WaitForQueueCountToBecome(IPendingRequestQueue pendingRequestQueue, int count)
        {
            while (pendingRequestQueue.Count != count)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken);
            }
        }

        Task<ResponseMessage> StartQueueAndWait(IPendingRequestQueue pendingRequestQueue, RequestMessage request, CancellationToken queueAndWaitCancellationToken)
        {
            var task = Task.Run(
                async () => await pendingRequestQueue.QueueAndWaitAsync(request, new RequestCancellationTokens(queueAndWaitCancellationToken, CancellationToken.None)),
                CancellationToken);
            return task;
        }

        async Task<(Task<ResponseMessage> queueAndWaitTask, RequestMessage dequeued)> QueueAndDequeueRequest_ForTimeoutTestingOnly_ToCopeWithRaceCondition(
            IPendingRequestQueue sut, 
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            //For most tests, this is not a good method to use. It is a fix for some specific tests to cope with a race condition when Team City runs out of resources (and causes tests to become flaky)

            while (true)
            {
                var queueAndWaitTask = await StartQueueAndWaitAndWaitForRequestToBeQueued(sut, request, CancellationToken);
                sut.Count.Should().Be(1, "Item should be queued");

                var dequeued = await sut.DequeueAsync(cancellationToken);
                sut.Count.Should().Be(0, "Item should be dequeued");

                // There is a race condition where the task/thread that queues the request can actually progress far enough that it times out before DequeueAsync can take the request.
                // This tends to happen in tests where PollingRequestQueueTimeout has been reduced.
                // If this happens, then the item is 'completed', and DequeueAsync returns null (not the state we wish to be in)
                // So if dequeued is null, then try again.
                if (dequeued is not null)
                {
                    return (queueAndWaitTask, dequeued);
                }

                cancellationToken.ThrowIfCancellationRequested();
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
                .Select((r,i) => Task.Run(async () => await sut.ApplyResponse(expectedResponsesInOrder[i], r.Destination)))
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
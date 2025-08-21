#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Logging;
using Halibut.Queue;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.Exceptions;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.NodeHeartBeat;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Protocol;
using Halibut.Util;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Queue.Redis
{
    [RedisTest]
    public class RedisPendingRequestQueueFixture : BaseTest
    {
        [Test]
        public async Task DequeueAsync_ShouldReturnRequestFromRedis()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var sut = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, CreateMessageSerialiserAndDataStreamStorage(), new HalibutTimeoutsAndLimits());
            await sut.WaitUntilQueueIsSubscribedToReceiveMessages();

            var task = sut.QueueAndWaitAsync(request, CancellationToken.None);

            // Act
            var result = await sut.DequeueAsync(CancellationToken);

            // Assert
            result.Should().NotBeNull();
            result!.RequestMessage.Id.Should().Be(request.Id);
            result.RequestMessage.MethodName.Should().Be(request.MethodName);
            result.RequestMessage.ServiceName.Should().Be(request.ServiceName);
        }

        [Test]
        public async Task WhenThePickupTimeoutExpires_AnErrorsIsReturnedAndTheRequestCanNotBeCollected()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);

            var request = new RequestMessageBuilder("poll://test-endpoint")
                .WithServiceEndpoint(b => b.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(100)))
                .Build();

            var sut = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, CreateMessageSerialiserAndDataStreamStorage(), new HalibutTimeoutsAndLimits());
            await sut.WaitUntilQueueIsSubscribedToReceiveMessages();

            // Act
            var response = await sut.QueueAndWaitAsync(request, CancellationToken.None);
            var result = await sut.DequeueAsync(CancellationToken);

            // Assert
            response.Error!.Message.Should().Contain("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time");
            result.Should().BeNull();
        }

        [Test]
        public async Task FullSendAndReceiveShouldWork()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var node1Sender = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, CreateMessageSerialiserAndDataStreamStorage(), new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, CreateMessageSerialiserAndDataStreamStorage(), new HalibutTimeoutsAndLimits());
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();

            // Act
            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await node2Receiver.DequeueAsync(CancellationToken);

            requestMessageWithCancellationToken.Should().NotBeNull();
            requestMessageWithCancellationToken!.RequestMessage.Id.Should().Be(request.Id);
            requestMessageWithCancellationToken.RequestMessage.MethodName.Should().Be(request.MethodName);
            requestMessageWithCancellationToken.RequestMessage.ServiceName.Should().Be(request.ServiceName);

            var response = ResponseMessage.FromResult(requestMessageWithCancellationToken.RequestMessage, "Yay");
            await node2Receiver.ApplyResponse(response, requestMessageWithCancellationToken.RequestMessage.ActivityId);

            var responseMessage = await queueAndWaitAsync;

            // Assert
            responseMessage.Result.Should().Be("Yay");
        }
        
        [Test]
        public async Task FullSendAndReceiveWithDataStreamShouldWork()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage();

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            request.Params = new[] { new ComplexObjectMultipleDataStreams(DataStream.FromString("hello"), DataStream.FromString("world")) };

            var node1Sender = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();

            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await node2Receiver.DequeueAsync(CancellationToken);

            var objWithDataStreams = (ComplexObjectMultipleDataStreams)requestMessageWithCancellationToken!.RequestMessage.Params[0];
            (await objWithDataStreams.Payload1!.ReadAsString(CancellationToken)).Should().Be("hello");
            (await objWithDataStreams.Payload2!.ReadAsString(CancellationToken)).Should().Be("world");

            var response = ResponseMessage.FromResult(requestMessageWithCancellationToken.RequestMessage,
                new ComplexObjectMultipleDataStreams(DataStream.FromString("good"), DataStream.FromString("bye")));

            await node2Receiver.ApplyResponse(response, requestMessageWithCancellationToken.RequestMessage.ActivityId);

            var responseMessage = await queueAndWaitAsync;

            var returnObject = (ComplexObjectMultipleDataStreams)responseMessage.Result!;
            (await returnObject.Payload1!.ReadAsString(CancellationToken)).Should().Be("good");
            (await returnObject.Payload2!.ReadAsString(CancellationToken)).Should().Be("bye");
        }

        [Test]
        public async Task WhenReadingTheResponseFromTheQueueFails_TheQueueAndWaitTaskReturnsAnUnknownError()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage()
                .ThrowsOnReadResponse(() => new OperationCanceledException());

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queue = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await queue.WaitUntilQueueIsSubscribedToReceiveMessages();

            // Act
            var queueAndWaitAsync = queue.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await queue.DequeueAsync(CancellationToken);
            await queue.ApplyResponse(ResponseMessage.FromResult(requestMessageWithCancellationToken!.RequestMessage, "Yay"),
                requestMessageWithCancellationToken.RequestMessage.ActivityId);

            var responseMessage = await queueAndWaitAsync;
            
            // Assert
            responseMessage.Error.Should().NotBeNull();
            CreateExceptionFromResponse(responseMessage, HalibutLog).IsRetryableError().Should().Be(HalibutRetryableErrorType.IsRetryable);
        }

        [Test]
        public async Task WhenEnteringTheQueue_AndRedisIsUnavailable_ARetryableExceptionIsThrown()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());

            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder);
            redisFacade.MaxDurationToRetryFor = TimeSpan.FromSeconds(1);

            var redisTransport = new HalibutRedisTransport(redisFacade);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            var queue = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, CreateMessageSerialiserAndDataStreamStorage(), new HalibutTimeoutsAndLimits());
            portForwarder.EnterKillNewAndExistingConnectionsMode();

            // Act
            var exception = await AssertThrowsAny.Exception(async () => await queue.QueueAndWaitAsync(request, CancellationToken.None));
            
            // Assert
            exception.IsRetryableError().Should().Be(HalibutRetryableErrorType.IsRetryable);
            exception.Message.Should().Contain("ailed since an error occured inserting the data into the queue");
        }

        [Test]
        public async Task WhenEnteringTheQueue_AndRedisIsUnavailableAndDataLoseOccurs_ARetryableExceptionIsThrown()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());

            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder, null);
            redisFacade.MaxDurationToRetryFor = TimeSpan.FromSeconds(1);

            var redisDataLoseDetector = new CancellableDataLossWatchForRedisLosingAllItsData();

            var redisTransport = Substitute.ForPartsOf<HalibutRedisTransportWithVirtuals>(new HalibutRedisTransport(redisFacade));
            redisTransport.Configure().PutRequest(Arg.Any<Uri>(), Arg.Any<Guid>(), Arg.Any<RedisStoredMessage>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    await redisDataLoseDetector.DataLossHasOccured();
                    throw new OperationCanceledException();
                });

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queue = new RedisPendingRequestQueue(endpoint, redisDataLoseDetector, HalibutLog, redisTransport, CreateMessageSerialiserAndDataStreamStorage(), new HalibutTimeoutsAndLimits());

            // Act
            var exception = await AssertThrowsAny.Exception(async () => await queue.QueueAndWaitAsync(request, CancellationToken.None));
            
            // Assert
            exception.IsRetryableError().Should().Be(HalibutRetryableErrorType.IsRetryable);
            exception.Message.Should().Contain("was cancelled because we detected that redis lost all of its data.");
        }
        
        [Test]
        public async Task WhenTheRequestReceiverNodeDetectsRedisDataLose_AndTheRequestSenderDoesNotYetDetectDataLose_TheRequestSenderNodeReturnsARetryableResponse()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var guid = Guid.NewGuid();
            
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage();

            await using var stableConnection = RedisFacadeBuilder.CreateRedisFacade(prefix:  guid);

            var redisDataLoseDetectorOnReceiver = new CancellableDataLossWatchForRedisLosingAllItsData();
            var node1Sender = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(stableConnection), messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, redisDataLoseDetectorOnReceiver, HalibutLog, new HalibutRedisTransport(stableConnection), messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            var queueAndWaitTask = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            var dequeuedRequest = await node2Receiver.DequeueAsync(CancellationToken);

            // Act
            await redisDataLoseDetectorOnReceiver.DataLossHasOccured();

            var responseToSendBack = CreateNonRetryableErrorResponse(dequeuedRequest);

            await node2Receiver.ApplyResponse(responseToSendBack, dequeuedRequest!.RequestMessage.ActivityId);

            var response = await queueAndWaitTask;
            response.Error.Should().NotBeNull();

            // Assert
            CreateExceptionFromResponse(response, HalibutLog)
                .IsRetryableError().Should().Be(HalibutRetryableErrorType.IsRetryable);
        }

        ResponseMessage CreateNonRetryableErrorResponse(RequestMessageWithCancellationToken? dequeuedRequest)
        {
            var responseThatWouldNotBeRetried = ResponseMessage.FromException(dequeuedRequest!.RequestMessage, new NoMatchingServiceOrMethodHalibutClientException(""));
            CreateExceptionFromResponse(responseThatWouldNotBeRetried, HalibutLog)
                .IsRetryableError().Should().Be(HalibutRetryableErrorType.NotRetryable);
            return responseThatWouldNotBeRetried;
        }

        [Test]
        public async Task WhenPreparingRequestFails_ARetryableExceptionIsThrown()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();

            var redisTransport = new HalibutRedisTransport(redisFacade);
            
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage()
                .ThrowsOnPrepareRequest(() => new OperationCanceledException());

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            var queue = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());

            // Act Assert
            var exception = await AssertThrowsAny.Exception(async () => await queue.QueueAndWaitAsync(request, CancellationToken.None));
            exception.IsRetryableError().Should().Be(HalibutRetryableErrorType.IsRetryable);
            exception.Message.Should().Contain("error occured when preparing request for queue");
        }

        [Test]
        public async Task WhenDataLostIsDetected_InFlightRequestShouldBeAbandoned_AndARetryableExceptionIsThrown()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage();

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            await using var dataLossWatcher = new CancellableDataLossWatchForRedisLosingAllItsData();

            var node1Sender = new RedisPendingRequestQueue(endpoint, dataLossWatcher, HalibutLog, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, dataLossWatcher, HalibutLog, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();

            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await node2Receiver.DequeueAsync(CancellationToken);

            requestMessageWithCancellationToken.Should().NotBeNull();

            // Act
            await dataLossWatcher.DataLossHasOccured();

            // Assert
            requestMessageWithCancellationToken!.CancellationToken.IsCancellationRequested.Should().BeTrue("The receiver of the data should just give up processing");

            // Verify that queueAndWaitAsync quickly returns with an error when data lose has occured.
            await Task.WhenAny(Task.Delay(5000), queueAndWaitAsync);

            queueAndWaitAsync.IsCompleted.Should().BeTrue("As soon as data loss is detected the queueAndWait should return.");

            // Sigh it can go down either of these paths!
            var e = await AssertException.Throws<Exception>(queueAndWaitAsync);
            e.And.IsRetryableError().Should().Be(HalibutRetryableErrorType.IsRetryable);
            e.And.Should().BeOfType<RedisDataLossHalibutClientException>();
        }

        [Test]
        public async Task OnceARequestIsComplete_NoInflightDisposableShouldExist()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage();

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queue = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await queue.WaitUntilQueueIsSubscribedToReceiveMessages();

            // Act
            var queueAndWaitAsync = queue.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await queue.DequeueAsync(CancellationToken);
            requestMessageWithCancellationToken.Should().NotBeNull();

            var response = ResponseMessage.FromResult(requestMessageWithCancellationToken!.RequestMessage, "Yay");
            await queue.ApplyResponse(response, requestMessageWithCancellationToken.RequestMessage.ActivityId);

            var responseMessage = await queueAndWaitAsync;
            responseMessage.Result.Should().Be("Yay");

            // Assert
            queue.DisposablesForInFlightRequests.Should().BeEmpty();
        }

        [Test]
        public async Task OnceARequestIsComplete_NoRequestSenderNodeHeartBeatsShouldBeSent()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queue = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, CreateMessageSerialiserAndDataStreamStorage(), new HalibutTimeoutsAndLimits());
            await queue.WaitUntilQueueIsSubscribedToReceiveMessages();
            queue.RequestSenderNodeHeartBeatRate = TimeSpan.FromSeconds(1);

            // Act
            var queueAndWaitAsync = queue.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await queue.DequeueAsync(CancellationToken);
            requestMessageWithCancellationToken.Should().NotBeNull();

            var response = ResponseMessage.FromResult(requestMessageWithCancellationToken!.RequestMessage, "Yay");
            await queue.ApplyResponse(response, requestMessageWithCancellationToken.RequestMessage.ActivityId);

            var responseMessage = await queueAndWaitAsync;
            responseMessage.Result.Should().Be("Yay");

            // Assert
            var heartBeatSent = false;
            var cts = new CancelOnDisposeCancellationToken();
            using var _ = redisTransport.SubscribeToNodeHeartBeatChannel(endpoint, request.ActivityId, HalibutQueueNodeSendingPulses.RequestSenderNode, async () =>
                {
                    await Task.CompletedTask;
                    heartBeatSent = true;
                },
                cts.Token);

            await Task.Delay(5000);
            heartBeatSent.Should().BeFalse();
        }

        [Test]
        public async Task OnceARequestIsComplete_NoRequestProcessorNodeHeartBeatsShouldBeSent()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queue = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, redisTransport, CreateMessageSerialiserAndDataStreamStorage(), new HalibutTimeoutsAndLimits());
            await queue.WaitUntilQueueIsSubscribedToReceiveMessages();
            queue.RequestReceiverNodeHeartBeatRate = TimeSpan.FromSeconds(1);

            // Act
            var queueAndWaitAsync = queue.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await queue.DequeueAsync(CancellationToken);
            requestMessageWithCancellationToken.Should().NotBeNull();

            var response = ResponseMessage.FromResult(requestMessageWithCancellationToken!.RequestMessage, "Yay");
            await queue.ApplyResponse(response, requestMessageWithCancellationToken.RequestMessage.ActivityId);

            var responseMessage = await queueAndWaitAsync;
            responseMessage.Result.Should().Be("Yay");

            // Assert
            var heartBeatSent = false;
            var cts = new CancelOnDisposeCancellationToken();
            using var _ = redisTransport.SubscribeToNodeHeartBeatChannel(endpoint, request.ActivityId, HalibutQueueNodeSendingPulses.RequestProcessorNode, async () =>
                {
                    await Task.CompletedTask;
                    heartBeatSent = true;
                },
                cts.Token);

            await Task.Delay(5000);
            heartBeatSent.Should().BeFalse();
        }

        [Test]
        public async Task WhenTheRequestProcessorNodeConnectionToRedisIsInterrupted_AndRestoredBeforeWorkIsPublished_TheReceiverShouldBeAbleToCollectThatWorkQuickly()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var guid = Guid.NewGuid();
            await using var redisFacadeSender = RedisFacadeBuilder.CreateRedisFacade(prefix: guid);

            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            await using var unstableRedisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder, guid);
            
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage();

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var highDequeueTimoueHalibutLimits = new HalibutTimeoutsAndLimits();
            highDequeueTimoueHalibutLimits.PollingQueueWaitTimeout = TimeSpan.FromDays(1); // We should not need to rely on the timeout working for very short disconnects.

            var requestSenderQueue = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(redisFacadeSender), messageReaderWriter, highDequeueTimoueHalibutLimits);
            var requestProcessQueueWithUnstableConnection = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(unstableRedisFacade), messageReaderWriter, highDequeueTimoueHalibutLimits);
            await requestProcessQueueWithUnstableConnection.WaitUntilQueueIsSubscribedToReceiveMessages();
            var dequeueTask = requestProcessQueueWithUnstableConnection.DequeueAsync(CancellationToken);

            await Task.Delay(5000, CancellationToken); // Allow some time for the receiver to subscribe to work.
            dequeueTask.IsCompleted.Should().BeFalse("Dequeue should not have ");

            portForwarder.EnterKillNewAndExistingConnectionsMode();
            await Task.Delay(1000, CancellationToken); // The network outage continues!

            portForwarder.ReturnToNormalMode(); // The network outage gets all fixed up :D
            Logger.Information("Network restored!");

            // The receiver should be able to get itself back into a state where,
            // new RequestMessages that are published are quickly collected.
            // However first we allow some time for the subscriptions to re-connect to redis,
            // we don't know how long that will take so give it what feels like too much time.
            await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken);

            var queueAndWaitAsync = requestSenderQueue.QueueAndWaitAsync(request, CancellationToken.None);

            // Surely it will be done in 25s, it should take less than 1s.
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(20), CancellationToken), dequeueTask);

            dequeueTask.IsCompleted.Should().BeTrue("The queue did not app");

            var requestReceived = await dequeueTask;
            requestReceived.Should().NotBeNull();
            requestReceived!.RequestMessage.ActivityId.Should().Be(request.ActivityId);
        }

        /// <summary>
        /// We want to check that the queue doesn't do something like:
        /// - place work on the queue
        /// - not receive a heart beat from the RequestProcessorNode, because the request is not yet collected.
        /// - timeout because we did not receive that heart beat.
        /// </summary>
        [Test]
        public async Task WhenTheReceiverDoesntCollectWorkImmediately_TheRequestCanSitOnTheQueueForSometime_AndBeOnTheQueueLongerThanTheHeartBeatTimeout()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();

            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage();

            var node1Sender = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(redisFacade), messageReaderWriter, new HalibutTimeoutsAndLimits());
            // We are testing that we don't expect heart beats before the request is collected.
            node1Sender.RequestReceiverNodeHeartBeatTimeout = TimeSpan.FromSeconds(1);
            await node1Sender.WaitUntilQueueIsSubscribedToReceiveMessages();

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            request.Destination.PollingRequestQueueTimeout = TimeSpan.FromHours(1);
            await using var cts = new CancelOnDisposeCancellationToken(CancellationToken);

            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, cts.Token);

            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5), CancellationToken), queueAndWaitAsync);

            queueAndWaitAsync.IsCompleted.Should().BeFalse();
        }

        [Test]
        public async Task WhenTheSendersConnectionToRedisIsBrieflyInterruptedWhileSendingTheRequestMessageToRedis_TheWorkIsStillSent()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var guid = Guid.NewGuid();
            await using var redisFacadeReceiver = RedisFacadeBuilder.CreateRedisFacade(prefix:  guid);

            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            await using var redisFacadeSender = RedisFacadeBuilder.CreateRedisFacade(portForwarder, guid);
            
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage();

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var node1Sender = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(redisFacadeSender), messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(redisFacadeSender), messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();

            portForwarder.EnterKillNewAndExistingConnectionsMode();

            var networkRestoreTask = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken);
                portForwarder.ReturnToNormalMode();
            });

            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            var dequeuedRequest = await node2Receiver.DequeueAsync(CancellationToken);

            dequeuedRequest.Should().NotBeNull();
            dequeuedRequest!.RequestMessage.ActivityId.Should().Be(request.ActivityId);
        }

        [Test]
        public async Task WhenTheRequestProcessorNodeDequeuesWork_AndThenDisconnectsFromRedisForEver_TheRequestSenderNodeEventuallyTimesOut()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var guid = Guid.NewGuid();
            
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage();

            var halibutTimeoutAndLimits = new HalibutTimeoutsAndLimits();
            halibutTimeoutAndLimits.PollingRequestQueueTimeout = TimeSpan.FromDays(1);
            halibutTimeoutAndLimits.PollingQueueWaitTimeout = TimeSpan.FromDays(1); // We should not need to rely on the timeout working for very short disconnects.

            await using var stableRedisConnection = RedisFacadeBuilder.CreateRedisFacade(prefix:  guid);

            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            await using var unstableRedisConnection = RedisFacadeBuilder.CreateRedisFacade(portForwarder, guid);

            var node1Sender = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(stableRedisConnection), messageReaderWriter, halibutTimeoutAndLimits);
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(unstableRedisConnection), messageReaderWriter, halibutTimeoutAndLimits);
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();

            // Lower this to complete the test sooner.
            node1Sender.RequestReceiverNodeHeartBeatRate = TimeSpan.FromSeconds(1);
            node2Receiver.RequestReceiverNodeHeartBeatRate = TimeSpan.FromSeconds(1);
            node1Sender.RequestReceiverNodeHeartBeatTimeout = TimeSpan.FromSeconds(10);
            node2Receiver.RequestReceiverNodeHeartBeatTimeout = TimeSpan.FromSeconds(10);

            // Act
            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            // Setting this low shows we don't timeout because the request was not picked up in time.
            request.Destination.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
            var queueAndWaitTask = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            var dequeuedRequest = await node2Receiver.DequeueAsync(CancellationToken);

            // Now disconnect the receiver from redis.
            portForwarder.EnterKillNewAndExistingConnectionsMode();

            // Assert
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(20), CancellationToken), queueAndWaitTask);

            queueAndWaitTask.IsCompleted.Should().BeTrue();

            var response = await queueAndWaitTask;
            response.Error.Should().NotBeNull();
            response.Error!.Message.Should().Contain("The node processing the request did not send a heartbeat for long enough, and so the node is now assumed to be offline.");

            CreateExceptionFromResponse(response, HalibutLog).IsRetryableError().Should().Be(HalibutRetryableErrorType.IsRetryable);
        }

        [Test]
        public async Task WhenTheRequestProcessorNodeDequeuesWork_AndTheRequestSenderNodeDisconnects_AndNeverReconnects_TheDequeuedWorkIsEventuallyCancelled()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var guid = Guid.NewGuid();
            
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage();

            var halibutTimeoutAndLimits = new HalibutTimeoutsAndLimits();
            halibutTimeoutAndLimits.PollingRequestQueueTimeout = TimeSpan.FromDays(1);
            halibutTimeoutAndLimits.PollingQueueWaitTimeout = TimeSpan.FromDays(1); // We should not need to rely on the timeout working for very short disconnects.

            await using var stableRedisConnection = RedisFacadeBuilder.CreateRedisFacade(prefix:  guid);

            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            await using var unstableRedisConnection = RedisFacadeBuilder.CreateRedisFacade(portForwarder, guid);

            var node1Sender = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(unstableRedisConnection), messageReaderWriter, halibutTimeoutAndLimits);
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(stableRedisConnection), messageReaderWriter, halibutTimeoutAndLimits);
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();

            node1Sender.RequestSenderNodeHeartBeatRate = TimeSpan.FromSeconds(1);
            node2Receiver.RequestSenderNodeHeartBeatRate = TimeSpan.FromSeconds(1);
            node1Sender.RequestSenderNodeHeartBeatTimeout = TimeSpan.FromSeconds(10);
            node2Receiver.RequestSenderNodeHeartBeatTimeout = TimeSpan.FromSeconds(10);
            node1Sender.TimeBetweenCheckingIfRequestWasCollected = TimeSpan.FromSeconds(1);
            node2Receiver.TimeBetweenCheckingIfRequestWasCollected = TimeSpan.FromSeconds(1);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queueAndWaitTask = node1Sender.QueueAndWaitAsync(request, CancellationToken);

            var dequeuedRequest = await node2Receiver.DequeueAsync(CancellationToken);
            dequeuedRequest!.CancellationToken.IsCancellationRequested.Should().BeFalse();

            // Now disconnect the sender from redis.
            portForwarder.EnterKillNewAndExistingConnectionsMode();

            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(35), dequeuedRequest.CancellationToken));

            dequeuedRequest.CancellationToken.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public async Task WhenTheRequestSenderNodeBrieflyDisconnectsFromRedis_AtExactlyTheTimeWhenTheRequestReceiverNodeSendsTheResponseBack_TheRequestSenderNodeStillGetsTheResponse()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var guid = Guid.NewGuid();
            
            var messageReaderWriter = CreateMessageSerialiserAndDataStreamStorage();

            await using var stableConnection = RedisFacadeBuilder.CreateRedisFacade(prefix:  guid);
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            await using var unreliableConnection = RedisFacadeBuilder.CreateRedisFacade(portForwarder, guid);

            var node1Sender = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(unreliableConnection), messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), HalibutLog, new HalibutRedisTransport(stableConnection), messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            var queueAndWaitTask = node1Sender.QueueAndWaitAsync(request, CancellationToken);

            var dequeuedRequest = await node2Receiver.DequeueAsync(CancellationToken);

            // Just before we send the response, disconnect the sender. 
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            await node2Receiver.ApplyResponse(ResponseMessage.FromResult(dequeuedRequest!.RequestMessage, "Yay"), dequeuedRequest!.RequestMessage.ActivityId);

            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);
            portForwarder.ReturnToNormalMode();

            await Task.WhenAny(Task.Delay(TimeSpan.FromMinutes(2), CancellationToken), queueAndWaitTask);

            queueAndWaitTask.IsCompleted.Should().BeTrue();

            var response = await queueAndWaitTask;
            response.Error.Should().BeNull();
            response.Result.Should().Be("Yay");
        }

        static Exception CreateExceptionFromResponse(ResponseMessage responseThatWouldNotBeRetried, ILog log)
        {
            try
            {
                HalibutProxyWithAsync.ThrowExceptionFromReceivedError(responseThatWouldNotBeRetried.Error!, log);
            }
            catch (Exception e)
            {
                return e;
            }

            Assert.Fail("Excpected an exception in the response message");
            throw new Exception("it failed");
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testWebSocket: false)]
        public async Task WhenUsingTheRedisQueue_ASimpleEchoServiceCanBeCalled(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithPendingRequestQueueFactory((queueMessageSerializer, logFactory) =>
                                 new RedisPendingRequestQueueFactory(
                                         queueMessageSerializer,
                                         dataStreamStore,
                                         new RedisNeverLosesData(),
                                         redisTransport,
                                         new HalibutTimeoutsAndLimits(),
                                         logFactory)
                                     .WithWaitForReceiverToBeReady())
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                (await echo.SayHelloAsync("Deploy package A")).Should().Be("Deploy package A...");

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++) (await echo.SayHelloAsync($"Deploy package A {i}")).Should().Be($"Deploy package A {i}...");
            }
        }

        [Test]
        public async Task CancellingARequestShouldResultInTheDequeuedResponseTokenBeingCancelled()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageSerialiserAndDataStreamStorage(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            request.Params = new[] { new ComplexObjectMultipleDataStreams(DataStream.FromString("hello"), DataStream.FromString("world")) };

            var node1Sender = new RedisPendingRequestQueue(endpoint, new RedisNeverLosesData(), log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node1Sender.WaitUntilQueueIsSubscribedToReceiveMessages();

            using var cts = new CancellationTokenSource();

            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, cts.Token);

            var requestMessageWithCancellationToken = await node1Sender.DequeueAsync(CancellationToken);

            requestMessageWithCancellationToken!.CancellationToken.IsCancellationRequested.Should().BeFalse();

            await cts.CancelAsync();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), requestMessageWithCancellationToken.CancellationToken);
            }
            catch (TaskCanceledException)
            {
            }

            requestMessageWithCancellationToken!.CancellationToken.IsCancellationRequested.Should().BeTrue();
        }

        public class QueueMessageSerializerBuilder
        {
            public QueueMessageSerializer Build()
            {
                var typeRegistry = new TypeRegistry();
                typeRegistry.Register(typeof(IComplexObjectService));

                StreamCapturingJsonSerializer StreamCapturingSerializer()
                {
                    var settings = MessageSerializerBuilder.CreateSerializer();
                    var binder = new RegisteredSerializationBinder(typeRegistry);
                    settings.SerializationBinder = binder;
                    return new StreamCapturingJsonSerializer(settings);
                }

                return new QueueMessageSerializer(StreamCapturingSerializer);
            }
        }
        
        static MessageSerialiserAndDataStreamStorage CreateMessageSerialiserAndDataStreamStorage()
        {
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageSerialiserAndDataStreamStorage(messageSerializer, dataStreamStore);
            return messageReaderWriter;
        }
    }
}
#endif
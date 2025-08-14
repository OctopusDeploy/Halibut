using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Logging;
using Halibut.Queue;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis;
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
using Nito.AsyncEx;
using NSubstitute;
using NUnit.Framework;
using Octopus.TestPortForwarder;
using Serilog;
using DisposableCollection = Halibut.Util.DisposableCollection;
using ILog = Halibut.Diagnostics.ILog;
using Try = Halibut.Tests.Support.Try;

namespace Halibut.Tests.Queue.Redis
{
    public class RedisPendingRequestQueueFixture : BaseTest
    {
        const int redisPort = 6379;
        private static RedisFacade CreateRedisFacade(int? port = redisPort, Guid? guid = null) => new("localhost:" + port, (guid ?? Guid.NewGuid()).ToString(), new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));

        [Test]
        public async Task DequeueAsync_ShouldReturnRequestFromRedis()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var sut = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
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
        public async Task WhenThePickupTimeoutExpires_AnErrorsReturned_AndTheRequestCanNotBeCollected()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint")
                .WithServiceEndpoint(b => b.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(100)))
                .Build();

            var sut = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await sut.WaitUntilQueueIsSubscribedToReceiveMessages();

            var response = await sut.QueueAndWaitAsync(request, CancellationToken.None);

            response.Error!.Message.Should().Contain("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time");

            // Act
            var result = await sut.DequeueAsync(CancellationToken);

            result.Should().BeNull();
        }

        
        [Test]
        public async Task FullSendAndReceiveShouldWork()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var node1Sender = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();

            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await node2Receiver.DequeueAsync(CancellationToken);

            requestMessageWithCancellationToken.Should().NotBeNull();
            requestMessageWithCancellationToken!.RequestMessage.Id.Should().Be(request.Id);
            requestMessageWithCancellationToken.RequestMessage.MethodName.Should().Be(request.MethodName);
            requestMessageWithCancellationToken.RequestMessage.ServiceName.Should().Be(request.ServiceName);

            var response = ResponseMessage.FromResult(requestMessageWithCancellationToken.RequestMessage, "Yay");
            await node2Receiver.ApplyResponse(response, requestMessageWithCancellationToken.RequestMessage.ActivityId);

            var responseMessage = await queueAndWaitAsync;

            responseMessage.Result.Should().Be("Yay");
        }
        
        [Test]
        public async Task WhenDataLostIsDetected_InFlightRequestShouldBeAbandoned_AndARetryableExceptionIsThrown()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            await using var dataLossWatcher = new CancellableDataLossWatchForRedisLosingAllItsData();

            var node1Sender = new RedisPendingRequestQueue(endpoint, dataLossWatcher, log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, dataLossWatcher, log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();

            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await node2Receiver.DequeueAsync(CancellationToken);

            requestMessageWithCancellationToken.Should().NotBeNull();

            // Act
            await dataLossWatcher.DataLossHasOccured();

            // Assert
            requestMessageWithCancellationToken!.CancellationToken.IsCancellationRequested.Should().BeTrue("The receiver of the data should just give up processing");

            // We don't want to just await queueAndWaitAsync, since timeouts will kick in resulting in a response no matter what.
            // We cant to see that it quickly returns.
            await Task.WhenAny(Task.Delay(5000), queueAndWaitAsync);

            queueAndWaitAsync.IsCompleted.Should().BeTrue("As soon as data loss is detected the queueAndWait should return.");

            // Sigh it can go down either of these paths!
            var e = await AssertException.Throws<Exception>(queueAndWaitAsync);
            e.And.IsRetryableError().Should().Be(HalibutRetryableErrorType.IsRetryable);
            e.And.Should().BeOfType<RedisDataLoseHalibutClientException>();
            
        }
        
        [Test]
        public async Task OnceARequestIsComplete_NoInflightDisposableShouldExist()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queue = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
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
            queue.disposablesForInFlightRequests.Should().BeEmpty();
        }
        
        [Test]
        public async Task OnceARequestIsComplete_NoSenderHeartBeatsShouldBeSent()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queue = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await queue.WaitUntilQueueIsSubscribedToReceiveMessages();
            queue.DelayBetweenHeartBeatsForRequestSender = TimeSpan.FromSeconds(1);
            
            // Act
            var queueAndWaitAsync = queue.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await queue.DequeueAsync(CancellationToken);
            requestMessageWithCancellationToken.Should().NotBeNull();

            var response = ResponseMessage.FromResult(requestMessageWithCancellationToken!.RequestMessage, "Yay");
            await queue.ApplyResponse(response, requestMessageWithCancellationToken.RequestMessage.ActivityId);
            

            var responseMessage = await queueAndWaitAsync;
            responseMessage.Result.Should().Be("Yay");
            
            // Assert
            bool heartBeatSent = false;
            var cts = new CancelOnDisposeCancellationToken();
            using var _ = redisTransport.SubscribeToNodeHeartBeatChannel(endpoint, request.ActivityId, HalibutQueueNodeSendingPulses.Sender, async () =>
            {
                await Task.CompletedTask;
                heartBeatSent = true;
            }, 
                cts.Token);
            
            await Task.Delay(5000);
            heartBeatSent.Should().BeFalse();
        }
        
        [Test]
        public async Task OnceARequestIsComplete_NoReceiverHeartBeatsShouldBeSent()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queue = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await queue.WaitUntilQueueIsSubscribedToReceiveMessages();
            queue.DelayBetweenHeartBeatsForRequestProcessor = TimeSpan.FromSeconds(1);

            // Act
            var queueAndWaitAsync = queue.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await queue.DequeueAsync(CancellationToken);
            requestMessageWithCancellationToken.Should().NotBeNull();

            var response = ResponseMessage.FromResult(requestMessageWithCancellationToken!.RequestMessage, "Yay");
            await queue.ApplyResponse(response, requestMessageWithCancellationToken.RequestMessage.ActivityId);

            var responseMessage = await queueAndWaitAsync;
            responseMessage.Result.Should().Be("Yay");
            
            // Assert
            bool heartBeatSent = false;
            var cts = new CancelOnDisposeCancellationToken();
            using var _ = redisTransport.SubscribeToNodeHeartBeatChannel(endpoint, request.ActivityId, HalibutQueueNodeSendingPulses.Receiver, async () =>
            {
                await Task.CompletedTask;
                heartBeatSent = true;
            }, 
                cts.Token);
            
            await Task.Delay(5000);
            heartBeatSent.Should().BeFalse();
        }

        [Test]
        public async Task FullSendAndReceiveWithDataStreamShouldWork()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var senderLog = new TestContextLogCreator("QueueSender", LogLevel.Trace).CreateNewForPrefix("");
            var receiverLog = new TestContextLogCreator("ReceiverLog", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            request.Params = new[] { new ComplexObjectMultipleDataStreams(DataStream.FromString("hello"), DataStream.FromString("world")) };

            var node1Sender = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), senderLog, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), receiverLog, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
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
        public async Task WhenTheReceiversConnectionToRedisIsInterruptedAndRestoredBeforeWorkIsPublished_TheReceiverShouldBeAbleToCollectThatWorkQuickly()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            await using var redisFacadeSender = CreateRedisFacade(guid: guid);

            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            await using var redisFacadeReceiver = CreateRedisFacade(portForwarder.ListeningPort, guid: guid);
            
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var halibutTimeoutAndLimits = new HalibutTimeoutsAndLimits();
            halibutTimeoutAndLimits.PollingQueueWaitTimeout = TimeSpan.FromDays(1); // We should not need to rely on the timeout working for very short disconnects.
            
            var node1Sender = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(redisFacadeSender), messageReaderWriter, halibutTimeoutAndLimits);
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(redisFacadeReceiver), messageReaderWriter, halibutTimeoutAndLimits);
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();
            var dequeueTask = node2Receiver.DequeueAsync(CancellationToken);
            
            await Task.Delay(5000, CancellationToken); // Allow some time for the receiver to subscribe to work.
            dequeueTask.IsCompleted.Should().BeFalse("Dequeue should not have ");
            
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            await Task.Delay(1000, CancellationToken); // The network outage continues!
            
            portForwarder.ReturnToNormalMode(); // The network outage gets all fixed up :D
            Logger.Information("Network restored!");
            
            // The receiver should be able to get itself back into a state where it can collect messages quickly, within this time.
            await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken);
            
            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            // Surely it will be done in 25s, it should take less than 1s.
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(20), CancellationToken), dequeueTask);

            dequeueTask.IsCompleted.Should().BeTrue("The queue did not app");

            var requestReceived = await dequeueTask;
            requestReceived.Should().NotBeNull();
            requestReceived!.RequestMessage.ActivityId.Should().Be(request.ActivityId);
        }
        
        [Test]
        public async Task WhenTheReceiverDoesntCollectWorkImmediately_TheRequestCanSitOnTheQueueForSometime_AndBeOnTheQueueLongerThanTheHeartBeatTimeout()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            await using var redisFacade = CreateRedisFacade();
            
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var node1Sender = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(redisFacade), messageReaderWriter, new HalibutTimeoutsAndLimits());
            // We are testing that we don't expect heart beats before the request is collected.
            node1Sender.RequestReceivingNodeIsOfflineHeartBeatTimeout = TimeSpan.FromSeconds(1);
            await node1Sender.WaitUntilQueueIsSubscribedToReceiveMessages();
            
            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            request.Destination.PollingRequestQueueTimeout = TimeSpan.FromHours(1);
            await using var cts = new CancelOnDisposeCancellationToken(CancellationToken);
            
            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, cts.Token);
            
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5), CancellationToken), queueAndWaitAsync);

            queueAndWaitAsync.IsCompleted.Should().BeFalse();
        }

        [Test]
        public async Task WhenTheSendersConnectionToRedisIsBrieflyInterruptedWhileSendingWork_TheWorkIsStillSent()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            await using var redisFacadeReceiver = CreateRedisFacade(guid: guid);

            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            await using var redisFacadeSender = CreateRedisFacade(portForwarder.ListeningPort, guid: guid);
            
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var halibutTimeoutAndLimits = new HalibutTimeoutsAndLimits();
            halibutTimeoutAndLimits.PollingQueueWaitTimeout = TimeSpan.FromDays(1); // We should not need to rely on the timeout working for very short disconnects.

            
            var node1Sender = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(redisFacadeSender), messageReaderWriter, halibutTimeoutAndLimits);
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(redisFacadeSender), messageReaderWriter, halibutTimeoutAndLimits);
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
        public async Task WhenTheReceiverDequeuesWorkAndThenDisconnectsFromRedisForEver_TheSenderEventuallyTimesOut()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var halibutTimeoutAndLimits = new HalibutTimeoutsAndLimits();
            halibutTimeoutAndLimits.PollingRequestQueueTimeout = TimeSpan.FromDays(1);
            halibutTimeoutAndLimits.PollingQueueWaitTimeout = TimeSpan.FromDays(1); // We should not need to rely on the timeout working for very short disconnects.

            await using var stableRedisConnection = CreateRedisFacade(guid: guid);

            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            await using var unstableRedisConnection = CreateRedisFacade(portForwarder.ListeningPort, guid: guid);
            
            var node1Sender = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(stableRedisConnection), messageReaderWriter, halibutTimeoutAndLimits);
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(unstableRedisConnection), messageReaderWriter, halibutTimeoutAndLimits);
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();
            
            // Lower this to complete the test sooner.
            node1Sender.DelayBetweenHeartBeatsForRequestProcessor = TimeSpan.FromSeconds(1);
            node2Receiver.DelayBetweenHeartBeatsForRequestProcessor = TimeSpan.FromSeconds(1);
            node1Sender.RequestReceivingNodeIsOfflineHeartBeatTimeout = TimeSpan.FromSeconds(10);
            node2Receiver.RequestReceivingNodeIsOfflineHeartBeatTimeout = TimeSpan.FromSeconds(10);
            
            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            
            // TODO: Setting this low shows we don't timeout because the request was not picked up in time.
            // Could be its own test.
            request.Destination.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
            var queueAndWaitTask = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);
            
            var dequeuedRequest = await node2Receiver.DequeueAsync(CancellationToken);
            
            // Now disconnect the receiver from redis.
            portForwarder.EnterKillNewAndExistingConnectionsMode();

            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(20), CancellationToken), queueAndWaitTask);

            queueAndWaitTask.IsCompleted.Should().BeTrue();

            var response = await queueAndWaitTask;
            response.Error.Should().NotBeNull();
            response.Error!.Message.Should().Contain("The node processing the request did not send a heartbeat for long enough, and so the node is now assumed to be offline.");

            CreateExceptionFromResponse(response, log).IsRetryableError().Should().Be(HalibutRetryableErrorType.IsRetryable);
        }
        
        [Test]
        public async Task WhenTheReceiverDequeuesWork_AndTheSenderDisconnects_AndNeverReconnects_TheDequeuedWorkIsEventuallyCancelled()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var halibutTimeoutAndLimits = new HalibutTimeoutsAndLimits();
            halibutTimeoutAndLimits.PollingRequestQueueTimeout = TimeSpan.FromDays(1);
            halibutTimeoutAndLimits.PollingQueueWaitTimeout = TimeSpan.FromDays(1); // We should not need to rely on the timeout working for very short disconnects.

            await using var stableRedisConnection = CreateRedisFacade(guid: guid);

            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            await using var unstableRedisConnection = CreateRedisFacade(portForwarder.ListeningPort, guid: guid);
            
            var node1Sender = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(unstableRedisConnection), messageReaderWriter, halibutTimeoutAndLimits);
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(stableRedisConnection), messageReaderWriter, halibutTimeoutAndLimits);
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();
            
            node1Sender.DelayBetweenHeartBeatsForRequestSender= TimeSpan.FromSeconds(1);
            node2Receiver.DelayBetweenHeartBeatsForRequestSender= TimeSpan.FromSeconds(1);
            node1Sender.NodeIsOfflineHeartBeatTimeoutForRequestSender = TimeSpan.FromSeconds(15);
            node2Receiver.NodeIsOfflineHeartBeatTimeoutForRequestSender = TimeSpan.FromSeconds(15);
            node1Sender.TimeBetweenCheckingIfRequestWasCollected = TimeSpan.FromSeconds(1);
            node2Receiver.TimeBetweenCheckingIfRequestWasCollected = TimeSpan.FromSeconds(1);
            
            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            
            var queueAndWaitTask = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);
            
            var dequeuedRequest = await node2Receiver.DequeueAsync(CancellationToken);
            dequeuedRequest!.CancellationToken.IsCancellationRequested.Should().BeFalse();
            
            // Now disconnect the sender from redis.
            portForwarder.EnterKillNewAndExistingConnectionsMode();

            await Task.WhenAny(Task.Delay(TimeSpan.FromMinutes(2), dequeuedRequest.CancellationToken));


            dequeuedRequest.CancellationToken.IsCancellationRequested.Should().BeTrue();
        }
        
        [Test]
        public async Task WhenTheSenderBrieflyDisconnectsFromRedisRightWhenTheReceiverSendsTheResponseBack_TheSenderStillGetsTheResponse()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            await using var stableConnection = CreateRedisFacade(guid: guid);
            using var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(redisPort, Logger).Build();
            await using var unreliableConnection = CreateRedisFacade(portForwarder.ListeningPort, guid: guid);
            
            var node1Sender = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(unreliableConnection), messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(stableConnection), messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();
            
            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            // TODO: This only needs to be set because we do not detect the work was collected as soon as it has been collected.
            request.Destination.PollingRequestQueueTimeout = TimeSpan.FromDays(1);
            var queueAndWaitTask = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);
            
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
        
        [Test]
        public async Task WhenTheRequestReceiverDetectsRedisDataLose_AndTheRequestSenderDoesNotYetDetectDataLose_TheSenderReceivesARetryableResponse()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var guid = Guid.NewGuid();
            
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            await using var stableConnection = CreateRedisFacade(guid: guid);

            var redisDataLoseDetectorOnReceiver = new CancellableDataLossWatchForRedisLosingAllItsData();
            var node1Sender = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, new HalibutRedisTransport(stableConnection), messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Receiver = new RedisPendingRequestQueue(endpoint, redisDataLoseDetectorOnReceiver, log, new HalibutRedisTransport(stableConnection), messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node2Receiver.WaitUntilQueueIsSubscribedToReceiveMessages();
            
            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            var queueAndWaitTask = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);
            
            var dequeuedRequest = await node2Receiver.DequeueAsync(CancellationToken);
            
            // Act
            await redisDataLoseDetectorOnReceiver.DataLossHasOccured();

            var responseThatWouldNotBeRetried = ResponseMessage.FromException(dequeuedRequest!.RequestMessage, new NoMatchingServiceOrMethodHalibutClientException(""));
            CreateExceptionFromResponse(responseThatWouldNotBeRetried, log)
                .IsRetryableError().Should().Be(HalibutRetryableErrorType.NotRetryable);
            
            await node2Receiver.ApplyResponse(ResponseMessage.FromResult(dequeuedRequest!.RequestMessage, "Yay"), dequeuedRequest!.RequestMessage.ActivityId);

            var response = await queueAndWaitTask;
            response.Error.Should().NotBeNull();
            
            // Assert
            CreateExceptionFromResponse(response, log)
                .IsRetryableError().Should().Be(HalibutRetryableErrorType.IsRetryable);
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
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithPendingRequestQueueFactory((queueMessageSerializer, logFactory) =>
                                 new RedisPendingRequestQueueFactory(
                                     queueMessageSerializer,
                                     dataStreamStore,
                                     new NeverLosingDataWatchForRedisLosingAllItsData(),
                                     redisTransport,
                                     new HalibutTimeoutsAndLimits(),
                                     logFactory)
                                     .WithWaitForReceiverToBeReady())
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                (await echo.SayHelloAsync("Deploy package A")).Should().Be("Deploy package A...");

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    (await echo.SayHelloAsync($"Deploy package A {i}")).Should().Be($"Deploy package A {i}...");
                }
            }
        }
        
        

        
        [Test]
        public async Task CancellingARequestShouldResultInTheDequeuedResponseTokenBeingCancelled()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix("");
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            request.Params = new[] { new ComplexObjectMultipleDataStreams(DataStream.FromString("hello"), DataStream.FromString("world")) };

            var node1Sender = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            await node1Sender.WaitUntilQueueIsSubscribedToReceiveMessages();

            using var cts = new CancellationTokenSource();
            
            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, cts.Token);

            var requestMessageWithCancellationToken = await node1Sender.DequeueAsync(CancellationToken);
            
            requestMessageWithCancellationToken!.CancellationToken.IsCancellationRequested.Should().BeFalse();

            await cts.CancelAsync();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), requestMessageWithCancellationToken.CancellationToken);
            } catch (TaskCanceledException){}
            
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
    }
}
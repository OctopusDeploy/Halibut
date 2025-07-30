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
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Protocol;
using Nito.AsyncEx;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using DisposableCollection = Halibut.Util.DisposableCollection;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Queue.Redis
{
    public class RedisPendingRequestQueueFixture : BaseTest
    {
        private static RedisFacade CreateRedisFacade() => new("localhost", Guid.NewGuid().ToString(), new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));

        [Test]
        public async Task DequeueAsync_ShouldReturnRequestFromRedis()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var log = Substitute.For<ILog>();
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var sut = new RedisPendingRequestQueue(endpoint, log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());

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
            var log = Substitute.For<ILog>();
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint")
                .WithServiceEndpoint(b => b.WithPollingRequestQueueTimeout(TimeSpan.FromMilliseconds(100)))
                .Build();

            var sut = new RedisPendingRequestQueue(endpoint, log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());

            var response = await sut.QueueAndWaitAsync(request, CancellationToken.None);

            response.Error!.Message.Should().Contain("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time");

            // Act
            var result = await sut.DequeueAsync(CancellationToken);

            result.Should().BeNull();
        }

        //[Test]
        public async Task When100kTentaclesAreSubscribed_TheQueueStillWorks()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            var log = Substitute.For<ILog>();
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());

            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimits();

            await using var disposableCollection = new DisposableCollection();
            for (int i = 0; i < 300000; i++)
            {
                disposableCollection.Add(new RedisPendingRequestQueue(new Uri("poll://" + Guid.NewGuid()), log, redisTransport, messageReaderWriter, halibutTimeoutsAndLimits));
                if (i % 10000 == 0)
                {
                    Logger.Information("Up to: {i}", i);
                }
            }

            this.Logger.Fatal("Waiting");
            await Task.Delay(30000);
            this.Logger.Fatal("Done");

            for (int i = 0; i < 10; i++)
            {
                var request = new RequestMessageBuilder(endpoint.ToString()).Build();

                await using var sut = new RedisPendingRequestQueue(endpoint, log, new HalibutRedisTransport(CreateRedisFacade()), messageReaderWriter, halibutTimeoutsAndLimits);

                var resultTask = sut.DequeueAsync(CancellationToken);

                await Task.Delay(100);

                var sw = Stopwatch.StartNew();

                var task = sut.QueueAndWaitAsync(request, CancellationToken.None);

                var result = await resultTask;
                // Act

                // Assert
                result.Should().NotBeNull();
                result!.RequestMessage.Id.Should().Be(request.Id);
                result.RequestMessage.MethodName.Should().Be(request.MethodName);
                result.RequestMessage.ServiceName.Should().Be(request.ServiceName);
                Logger.Information("It took {F}", sw.Elapsed.TotalSeconds.ToString("0.00"));
            }
        }

        [Test]
        public async Task FullSendAndReceiveShouldWork()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = Substitute.For<ILog>();
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var node1Sender = new RedisPendingRequestQueue(endpoint, log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Reciever = new RedisPendingRequestQueue(endpoint, log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());

            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await node2Reciever.DequeueAsync(CancellationToken);

            requestMessageWithCancellationToken.Should().NotBeNull();
            requestMessageWithCancellationToken!.RequestMessage.Id.Should().Be(request.Id);
            requestMessageWithCancellationToken.RequestMessage.MethodName.Should().Be(request.MethodName);
            requestMessageWithCancellationToken.RequestMessage.ServiceName.Should().Be(request.ServiceName);

            var response = ResponseMessage.FromResult(requestMessageWithCancellationToken.RequestMessage, "Yay");
            await node2Reciever.ApplyResponse(response, requestMessageWithCancellationToken.RequestMessage.ActivityId);

            var responseMessage = await queueAndWaitAsync;

            responseMessage.Result.Should().Be("Yay");
        }

        [Test]
        public async Task FullSendAndReceiveWithDataStreamShouldWork()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = Substitute.For<ILog>();
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            request.Params = new[] { new ComplexObjectMultipleDataStreams(DataStream.FromString("hello"), DataStream.FromString("world")) };

            var node1Sender = new RedisPendingRequestQueue(endpoint, log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());
            var node2Reciever = new RedisPendingRequestQueue(endpoint, log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());

            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, CancellationToken.None);

            var requestMessageWithCancellationToken = await node2Reciever.DequeueAsync(CancellationToken);

            var objWithDataStreams = (ComplexObjectMultipleDataStreams)requestMessageWithCancellationToken!.RequestMessage.Params[0];
            (await objWithDataStreams.Payload1!.ReadAsString(CancellationToken)).Should().Be("hello");
            (await objWithDataStreams.Payload2!.ReadAsString(CancellationToken)).Should().Be("world");

            var response = ResponseMessage.FromResult(requestMessageWithCancellationToken.RequestMessage,
                new ComplexObjectMultipleDataStreams(DataStream.FromString("good"), DataStream.FromString("bye")));

            await node2Reciever.ApplyResponse(response, requestMessageWithCancellationToken.RequestMessage.ActivityId);

            var responseMessage = await queueAndWaitAsync;

            var returnObject = (ComplexObjectMultipleDataStreams)responseMessage.Result!;
            (await returnObject.Payload1!.ReadAsString(CancellationToken)).Should().Be("good");
            (await returnObject.Payload2!.ReadAsString(CancellationToken)).Should().Be("bye");
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false, testWebSocket: false)]
        public async Task OctopusCanSendMessagesToTentacle_WithEchoService(ClientAndServiceTestCase clientAndServiceTestCase)
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
                                     redisTransport,
                                     new HalibutTimeoutsAndLimits(),
                                     logFactory))
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
        public async Task CancellingARequestShouldResultInTheDequeuedResponseTokenBeingCanelled()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid().ToString());
            var log = Substitute.For<ILog>();
            var redisTransport = new HalibutRedisTransport(CreateRedisFacade());
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            request.Params = new[] { new ComplexObjectMultipleDataStreams(DataStream.FromString("hello"), DataStream.FromString("world")) };

            var node1Sender = new RedisPendingRequestQueue(endpoint, log, redisTransport, messageReaderWriter, new HalibutTimeoutsAndLimits());

            using var cts = new CancellationTokenSource();
            
            var queueAndWaitAsync = node1Sender.QueueAndWaitAsync(request, cts.Token);

            var requestMessageWithCancellationToken = await node1Sender.DequeueAsync(CancellationToken);
            
            requestMessageWithCancellationToken!.CancellationToken.IsCancellationRequested.Should().BeFalse();

            await cts.CancelAsync();

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), requestMessageWithCancellationToken.CancellationToken);
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
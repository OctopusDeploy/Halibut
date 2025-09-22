#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Halibut.Queue;
using Halibut.Queue.MessageStreamWrapping;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Tests.Builders;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Queue.Redis
{
    [RedisTest]
    public class MessageSerialiserAndDataStreamStorageExceptionObserverTest : BaseTest
    {
        [Test]
        public async Task WhenStoreDataStreamsThrows_ExceptionObserverShouldBeNotified()
        {
            // Arrange
            var endpoint = new Uri("poll://" + Guid.NewGuid());
            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade();
            var redisTransport = new HalibutRedisTransport(redisFacade);

            var expectedException = new InvalidOperationException("Test exception from storage");
            var throwingDataStreamStorage = new ThrowingStoreDataStreamsForDistributedQueues(expectedException);
            var testObserver = new TestMessageSerialiserAndDataStreamStorageExceptionObserver();

            var queueMessageSerializer = new QueueMessageSerializerBuilder().Build();
            var logFactory = new TestContextLogCreator("Redis", LogLevel.Trace).ToCachingLogFactory();
            var factory = new RedisPendingRequestQueueFactory(
                queueMessageSerializer,
                throwingDataStreamStorage,
                new RedisNeverLosesData(),
                redisTransport,
                new HalibutTimeoutsAndLimits(),
                logFactory,
                testObserver);

            var sut = (RedisPendingRequestQueue)factory.CreateQueue(endpoint);
            await sut.WaitUntilQueueIsSubscribedToReceiveMessages();

            // Create a request with data streams to trigger PrepareRequest -> StoreDataStreams
            var request = new RequestMessageBuilder("poll://test-endpoint").Build();
            request.Params = new[] { new ComplexObjectMultipleDataStreams(DataStream.FromString("hello"), DataStream.FromString("world")) };

            // Act & Assert
            var exception = await AssertThrowsAny.Exception(async () => await sut.QueueAndWaitAsync(request, CancellationToken));

            // Verify that the observer was called
            testObserver.ObservedException.Should().NotBeNull("Exception observer should have been called");
            testObserver.ObservedException.Should().BeSameAs(expectedException, "Observer should receive the original exception");
            testObserver.MethodName.Should().Be(nameof(IMessageSerialiserAndDataStreamStorage.PrepareRequest), "Observer should know which method threw the exception");

            // Verify the original exception is still thrown
            exception.Should().NotBeNull("Original exception should still be thrown");
            exception.InnerException.Should().BeSameAs(expectedException, "Original exception should be preserved");
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

                return new QueueMessageSerializer(StreamCapturingSerializer, new MessageStreamWrappers());
            }
        }

        class ThrowingStoreDataStreamsForDistributedQueues : IStoreDataStreamsForDistributedQueues
        {
            readonly Exception exceptionToThrow;

            public ThrowingStoreDataStreamsForDistributedQueues(Exception exceptionToThrow)
            {
                this.exceptionToThrow = exceptionToThrow;
            }

            public Task<byte[]> StoreDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken)
            {
                throw exceptionToThrow;
            }

            public Task RehydrateDataStreams(byte[] dataStreamMetadata, List<IRehydrateDataStream> rehydrateDataStreams, CancellationToken cancellationToken)
            {
                throw exceptionToThrow;
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }

        class TestMessageSerialiserAndDataStreamStorageExceptionObserver : IMessageSerialiserAndDataStreamStorageExceptionObserver
        {
            public Exception? ObservedException { get; private set; }
            public string? MethodName { get; private set; }

            public void OnException(Exception exception, string methodName)
            {
                ObservedException = exception;
                MethodName = methodName;
            }
        }
    }
}
#endif

using System;
using System.Linq;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Queue;
using Halibut.Tests.Support;
using Halibut.Transport.Protocol;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Halibut.Tests.Queue
{
    public class QueueMessageSerializerFixture : BaseTest
    {
        [Test]
        public void SerializeAndDeserializeMessage_ShouldRoundTrip()
        {
            // Arrange
            var sut = new QueueMessageSerializerBuilder(new LogFactory())
                .Build();

            const string testMessage = "Hello, Queue!";

            // Act
            var (json, dataStreams) = sut.WriteMessage(testMessage);
            var (deserializedMessage, deserializedDataStreams) = sut.ReadMessage<string>(json);

            // Assert
            deserializedMessage.Should().Be(testMessage);
            dataStreams.Should().BeEmpty();
            deserializedDataStreams.Should().BeEmpty();
        }
        
        [Test]
        public void SerializeAndDeserializeMessage_ShouldRoundTrip_RequestMessage()
        {
            // Arrange
            var sut = new QueueMessageSerializerBuilder(new LogFactory())
                .Build();

            var request = new RequestMessage()
            {
                Id = "hello",
                ActivityId = Guid.NewGuid(),
                Destination = new ServiceEndPoint(new Uri("poll://bob"), "n", new HalibutTimeoutsAndLimits()),
                ServiceName = "service",
                MethodName = "Echo",
                Params = new object[] {"hello"}
            };

            // Act
            var (json, dataStreams) = sut.WriteMessage(request);
            var (deserializedMessage, deserializedDataStreams) = sut.ReadMessage<RequestMessage>(json);

            // Assert
            deserializedMessage.Should().BeEquivalentTo(request);
            dataStreams.Should().BeEmpty();
            deserializedDataStreams.Should().BeEmpty();
        }

        public class QueueMessageSerializerBuilder
        {
            readonly ILogFactory logFactory;
            ITypeRegistry? typeRegistry;
            Action<JsonSerializerSettings>? configureSerializer;

            public QueueMessageSerializerBuilder(ILogFactory logFactory)
            {
                this.logFactory = logFactory;
            }

            public QueueMessageSerializerBuilder WithTypeRegistry(ITypeRegistry typeRegistry)
            {
                this.typeRegistry = typeRegistry;
                return this;
            }

            public QueueMessageSerializerBuilder WithSerializerSettings(Action<JsonSerializerSettings> configure)
            {
                configureSerializer = configure;
                return this;
            }

            public QueueMessageSerializer Build()
            {
                var typeRegistry = this.typeRegistry ?? new TypeRegistry();

                StreamCapturingJsonSerializer StreamCapturingSerializer()
                {
                    var settings = MessageSerializerBuilder.CreateSerializer();
                    var binder = new RegisteredSerializationBinder(typeRegistry);
                    settings.SerializationBinder = binder;
                    configureSerializer?.Invoke(settings);
                    return new StreamCapturingJsonSerializer(settings);
                }

                return new QueueMessageSerializer(StreamCapturingSerializer);
            }
        }
    }
} 
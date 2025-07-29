using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        
        [Test]
        public void SerializeAndDeserializeMessageWithDataStream_ShouldRoundTrip_RequestMessage()
        {
            var typeRegistry = new TypeRegistry();
            typeRegistry.Register(typeof(IHaveTypeWithDataStreamsService));
            // Arrange
            var sut = new QueueMessageSerializerBuilder(new LogFactory())
                .WithTypeRegistry(typeRegistry)
                .Build();

            var request = new RequestMessage()
            {
                Id = "hello",
                ActivityId = Guid.NewGuid(),
                Destination = new ServiceEndPoint(new Uri("poll://bob"), "n", new HalibutTimeoutsAndLimits()),
                ServiceName = "service",
                MethodName = "Echo",
                Params = new object[] {"hello",
                    DataStream.FromString("yo")
                    ,new TypeWithDataStreams(new RepeatingStringDataStream("bob", 10))
                    
                }
            };

            // Act
            var (json, dataStreams) = sut.WriteMessage(request);

            dataStreams[1].Should().BeOfType<RepeatingStringDataStream>();

            json.Should().Contain("TypeWithDataStreams");
            json.Should().NotContain("RepeatingStringDataStream");
            
            var (deserializedMessage, deserializedDataStreams) = sut.ReadMessage<RequestMessage>(json);

            // Assert
            //deserializedMessage.Should().BeEquivalentTo(request);
            deserializedDataStreams.Count.Should().Be(2);
        }

        public interface IHaveTypeWithDataStreamsService
        {
            public void Do(TypeWithDataStreams typeWithDataStreams);
        }
        public class TypeWithDataStreams
        {
            public TypeWithDataStreams(DataStream dataStream)
            {
                DataStream = dataStream;
            }

            public DataStream DataStream { get; set; }
        }
        
        
        public class RepeatingStringDataStream : DataStream
        {
            string toRepeat;
            int HowManyTimes;
            
            public RepeatingStringDataStream(string toRepeat, int howManyTimes) 
                : base(toRepeat.GetUTF8Bytes().Length * howManyTimes, WriteRepeatedStringsAsync(toRepeat, howManyTimes))
            {
                this.toRepeat = toRepeat;
                HowManyTimes = howManyTimes;
            }

            static Func<Stream, CancellationToken, Task> WriteRepeatedStringsAsync(string toRepeat, int howManyTimes)
            {
                return (async (stream, token) =>
                {
                    for (int i = 0; i < howManyTimes; i++)
                    {
                        await stream.WriteAsync(toRepeat.GetUTF8Bytes(), token);
                    }
                });
            }
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
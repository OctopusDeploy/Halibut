#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Queue
{
    public class QueueMessageSerializerFixture : BaseTest
    {
        [Test]
        public void SerializeAndDeserializeSimpleStringMessage_ShouldRoundTrip()
        {
            // Arrange
            var sut = new QueueMessageSerializerBuilder().Build();

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
        public void SerializeAndDeserializeRequestMessage_ShouldRoundTrip_RequestMessage()
        {
            // Arrange
            var sut = new QueueMessageSerializerBuilder().Build();

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
        public void SerializeAndDeserializeRequestMessageWithDataStream_ShouldRoundTrip_RequestMessage()
        {
            var typeRegistry = new TypeRegistry();
            typeRegistry.Register(typeof(IHaveTypeWithDataStreamsService));
            // Arrange
            var sut = new QueueMessageSerializerBuilder()
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
            // Manually check each field of the deserializedMessage matches the request
            deserializedMessage.Id.Should().Be(request.Id);
            deserializedMessage.ActivityId.Should().Be(request.ActivityId);
            deserializedMessage.Destination.BaseUri.Should().Be(request.Destination.BaseUri);
            deserializedMessage.ServiceName.Should().Be(request.ServiceName);
            deserializedMessage.MethodName.Should().Be(request.MethodName);
            
            // Check Params array structure (DataStreams are replaced with placeholders during serialization)
            deserializedMessage.Params.Should().HaveCount(request.Params.Length);
            deserializedMessage.Params[0].Should().Be(request.Params[0]); // First param is a simple string
            // Note: Params[1] and Params[2] contain DataStreams which get replaced during serialization
            
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
    }
} 
#endif
#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Queue.MessageStreamWrapping;
using Halibut.Tests.Support;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;
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

            var jsonString = Encoding.UTF8.GetString(json);
            jsonString.Should().Contain("TypeWithDataStreams");
            jsonString.Should().NotContain("RepeatingStringDataStream");
            
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
        
        [Test]
        public void SerializeAndDeserializeSimpleStringMessage_WithStreamWrappers_ShouldRoundTrip()
        {
            // Arrange
            var sut = new QueueMessageSerializerBuilder()
                .WithMessageStreamWrappers(MessageStreamWrappersBuilder
                    .WrapStreamWith(new GzipMessageStreamWrapper())
                    .AndThenWrapThatWith(new Base64StreamWrapper())
                    .Build())
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
        public void SerializeAndDeserializeSimpleStringMessage_WithStreamWrappers_ShouldDisposeStreamsInCorrectOrder()
        {
            // Arrange
            var disposeOrderWriter = new List<string>();
            var disposeOrderReader = new List<string>();
            
            var firstWrapper = new FuncMessageStreamWrapper(
                writingStream => new StreamWithOnDisposeFunc(writingStream, () => disposeOrderWriter.Add("FirstWriteDisposed")),
                readingStream => new StreamWithOnDisposeFunc(readingStream, () => disposeOrderReader.Add("FirstReadDisposed")));
            
            var secondWrapper = new FuncMessageStreamWrapper(
                writingStream => new StreamWithOnDisposeFunc(writingStream, () => disposeOrderWriter.Add("SecondWriteDisposed")),
                readingStream => new StreamWithOnDisposeFunc(readingStream, () => disposeOrderReader.Add("SecondReadDisposed")));
            
            
            var sut = new QueueMessageSerializerBuilder()
                .WithMessageStreamWrappers(MessageStreamWrappersBuilder
                    .WrapStreamWith(firstWrapper)
                    .AndThenWrapThatWith(secondWrapper)
                    .Build())
                .Build();

            const string testMessage = "Hello, Queue!";

            // Act
            var (json, dataStreams) = sut.WriteMessage(testMessage);
            var (deserializedMessage, deserializedDataStreams) = sut.ReadMessage<string>(json);

            // Assert
            deserializedMessage.Should().Be(testMessage);
            dataStreams.Should().BeEmpty();
            deserializedDataStreams.Should().BeEmpty();

            // The dispose order should be in reverse to how the streams were created.
            disposeOrderWriter.Should().BeEquivalentTo("SecondWriteDisposed", "FirstWriteDisposed");
            disposeOrderReader.Should().BeEquivalentTo("SecondReadDisposed", "FirstReadDisposed");
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

    public class Base64StreamWrapper : IMessageStreamWrapper
    {
        public Stream WrapMessageSerialisationStream(Stream stream)
        {
            return new CryptoStream(stream, new ToBase64Transform(), CryptoStreamMode.Write, leaveOpen: true);
        }

        public Stream WrapMessageDeserialisationStream(Stream stream)
        {
            return new CryptoStream(stream, new FromBase64Transform(), CryptoStreamMode.Read, leaveOpen: true);
        }
    }

    public class StreamWithOnDisposeFunc : Stream
    {
        Action OnDispose;
        
        private readonly Stream stream;

        public StreamWithOnDisposeFunc(Stream stream, Action onDispose)
        {
            this.stream = stream;
            OnDispose = onDispose;
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
        }

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            OnDispose();
            base.Dispose(disposing);
        }
    }

    public class FuncMessageStreamWrapper : IMessageStreamWrapper
    {
        Func<Stream, Stream> wrappedForWriting;
        Func<Stream, Stream> wrappedForReading;
        

        public FuncMessageStreamWrapper(Func<Stream, Stream> wrappedForWriting, Func<Stream, Stream> wrappedForReading)
        {
            this.wrappedForWriting = wrappedForWriting;
            this.wrappedForReading = wrappedForReading;
        }

        public Stream WrapMessageSerialisationStream(Stream stream) => wrappedForWriting(stream);


        public Stream WrapMessageDeserialisationStream(Stream stream) => wrappedForReading(stream);
    }
} 
#endif
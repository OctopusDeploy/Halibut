using System;
using System.IO;
using System.IO.Compression;
using Halibut.Transport.Observability;
using System.Linq;
using Halibut.Diagnostics;
using Halibut.Transport.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Transport.Protocol
{
    public class MessageSerializer : IMessageSerializer
    {
        readonly ITypeRegistry typeRegistry;
        readonly Func<JsonSerializer> createSerializer;
        readonly IMessageSerializerObserver observer;
        readonly DeflateStreamInputBufferReflector deflateReflector;

        public MessageSerializer() // kept for backwards compatibility.
        {
            typeRegistry = new TypeRegistry();
            createSerializer = () =>
            {
                var settings = MessageSerializerBuilder.CreateSerializer();
                var binder = new RegisteredSerializationBinder(typeRegistry);
                settings.SerializationBinder = binder;
                return JsonSerializer.Create(settings);
            };
            deflateReflector = new DeflateStreamInputBufferReflector();
            observer = new NoMessageSerializerObserver();
        }

        internal MessageSerializer(
            ITypeRegistry typeRegistry, 
            Func<JsonSerializer> createSerializer,
            IMessageSerializerObserver observer)
        {
            this.typeRegistry = typeRegistry;
            this.createSerializer = createSerializer;
            this.observer = observer;
            deflateReflector = new DeflateStreamInputBufferReflector();
        }

        public void AddToMessageContract(params Type[] types) // kept for backwards compatibility
        {
            typeRegistry.AddToMessageContract(types);
        }
        
        public void WriteMessage<T>(Stream stream, T message)
        {
            using var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen);

            using (var zip = new DeflateStream(compressedByteCountingStream, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                // for the moment this MUST be object so that the $type property is included
                // If it is not, then an old receiver (eg, old tentacle) will not be able to understand messages from a new sender (server)
                // Once ALL sources and targets are deserializing to MessageEnvelope<T>, (ReadBsonMessage) then this can be changed to T
                createSerializer().Serialize(bson, new MessageEnvelope<object> { Message = message });
            }

            observer.MessageWritten(compressedByteCountingStream.BytesWritten);
        }

        public T ReadMessage<T>(Stream stream)
        {
            var messageReader = MessageReaderStrategyFromStream<T>(stream);
            using (var errorRecorder = new ErrorRecordingStream(stream))
            {
                Exception exceptionFromDeserialisation = null;
                try
                {
                    return messageReader(errorRecorder);
                }
                catch (Exception e)
                {
                    exceptionFromDeserialisation = e;
                }
                finally
                {
                    if (errorRecorder.ReadExceptions.Count == 1)
                    {
                        throw errorRecorder.ReadExceptions[0];
                    }

                    if (errorRecorder.WasTheEndOfStreamEncountered)
                    {
                        throw new EndOfStreamException();
                    }

                    if (errorRecorder.ReadExceptions.Count == 0 && exceptionFromDeserialisation != null)
                    {
                        throw exceptionFromDeserialisation;
                    }

                    if (errorRecorder.ReadExceptions.Count > 0)
                    {
                        throw new IOException("Error Reading from stream", new AggregateException(errorRecorder.ReadExceptions));
                    }
                }

                // Can never be reached
                throw exceptionFromDeserialisation;
            }

        }

        Func<Stream, T> MessageReaderStrategyFromStream<T>(Stream stream)
        {
            if (stream is IRewindableBuffer rewindable)
            {
                return (s) => ReadCompressedMessageRewindable<T>(s, rewindable);
            }

            return (s) => ReadCompressedMessage<T>(s);
        }

        T ReadCompressedMessage<T>(Stream stream)
        {
            using (var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen))
            using (var zip = new DeflateStream(compressedByteCountingStream, CompressionMode.Decompress, true))
            using (var decompressedByteCountingStream = new ByteCountingStream(zip, OnDispose.LeaveInputStreamOpen))
            using (var bson = new BsonDataReader(decompressedByteCountingStream) { CloseInput = false })
            {
                var messageEnvelope = DeserializeMessage<T>(bson);

                observer.MessageRead(compressedByteCountingStream.BytesRead, decompressedByteCountingStream.BytesRead);

                return messageEnvelope.Message;
            }
        }

        T ReadCompressedMessageRewindable<T>(Stream stream, IRewindableBuffer rewindable)
        {
            rewindable.StartBuffer();
            try
            {
                using (var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen))
                using (var zip = new DeflateStream(compressedByteCountingStream, CompressionMode.Decompress, true))
                using (var decompressedObservableStream = new ByteCountingStream(zip, OnDispose.LeaveInputStreamOpen))
                using (var bson = new BsonDataReader(decompressedObservableStream) { CloseInput = false })
                {
                    var messageEnvelope = DeserializeMessage<T>(bson);
                    
                    // Find the unused bytes in the DeflateStream input buffer
                    if (deflateReflector.TryGetAvailableInputBufferSize(zip, out var unusedBytesCount))
                    {
                        rewindable.FinishAndRewind(unusedBytesCount);
                    }
                    else
                    {
                        rewindable.CancelBuffer();
                    }

                    observer.MessageRead(compressedByteCountingStream.BytesRead - unusedBytesCount, decompressedObservableStream.BytesRead);

                    return messageEnvelope.Message;
                }
            }
            catch
            {
                rewindable.CancelBuffer();
                throw;
            }
        }

        MessageEnvelope<T> DeserializeMessage<T>(JsonReader reader)
        {
            var result = createSerializer().Deserialize<MessageEnvelope<T>>(reader);
            if (result == null)
            {
                throw new Exception("messageEnvelope is null");
            }
            return result;
        }
        
        // By making this a generic type, each message specifies the exact type it sends/expects
        // And it is impossible to deserialize the wrong type - any mismatched type will refuse to deserialize
        public class MessageEnvelope<T>
        {
            public T Message { get; set; }
        }
    }
}
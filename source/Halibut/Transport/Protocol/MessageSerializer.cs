using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Halibut.Transport.Observability;
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

        private void ReallyEndReading(DeflateStream zip)
        {
            var b = new byte[1024];
            var res = zip.Read(b, 0, b.Length);
        }

        CompressionLevel compressionLevel = CompressionLevel.NoCompression;
        
        public void WriteMessage<T>(Stream stream, T message)
        {
            using var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen);

            using (var zip = new DeflateStream(compressedByteCountingStream, compressionLevel, true))
            //using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            using (StreamWriter writer = new StreamWriter(zip, Encoding.UTF8, -1,  true))
            using (var bson = new JsonTextWriter(writer) { CloseOutput = false })
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
            if (stream is StreamAndRecord streamAndRecord)
            {
                if (streamAndRecord.stream is IRewindableBuffer rewindable2)
                {
                    return ReadCompressedMessageRewindable<T>(stream, rewindable2);
                }
            } 
            
            if (stream is IRewindableBuffer rewindable)
            {
                return ReadCompressedMessageRewindable<T>(stream, rewindable);
            }

            return ReadCompressedMessage<T>(stream);
        }

        T ReadCompressedMessage<T>(Stream stream)
        {
            using (var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen))
            using (var zip = new DeflateStream(compressedByteCountingStream, CompressionMode.Decompress, true))
            using (var decompressedByteCountingStream = new ByteCountingStream(zip, OnDispose.LeaveInputStreamOpen))
            //using (var bson = new BsonDataReader(decompressedByteCountingStream) { CloseInput = false })
            using (StreamReader reader = new StreamReader(zip, Encoding.UTF8, true, -1, true))
            using (var bson = new JsonTextReader(reader) {CloseInput = false})
            {
                var messageEnvelope = DeserializeMessage<T>(bson);

                observer.MessageRead(compressedByteCountingStream.BytesRead, decompressedByteCountingStream.BytesRead);
                
                // May be needed with no compression
                ReallyEndReading(zip);

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
                //using (var bson = new BsonDataReader(decompressedObservableStream) { CloseInput = false })
                using (StreamReader reader = new StreamReader(zip, Encoding.UTF8, true, -1, true))
                using (var bson = new JsonTextReader(reader) {CloseInput = false})
                {
                    var messageEnvelope = DeserializeMessage<T>(bson);
                    
                    // May be needed with no compression
                    ReallyEndReading(zip);
                    
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
        class MessageEnvelope<T>
        {
            public T Message { get; set; }
        }
    }
}
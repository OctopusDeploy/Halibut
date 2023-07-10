using System;
using System.IO;
using System.IO.Compression;
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
        
        public void WriteMessage<T>(Stream stream, T message)
        {
            using var compressedObservableStream = new ObservableStream(stream);

            using (var zip = new DeflateStream(compressedObservableStream, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                // for the moment this MUST be object so that the $type property is included
                // If it is not, then an old receiver (eg, old tentacle) will not be able to understand messages from a new sender (server)
                // Once ALL sources and targets are deserializing to MessageEnvelope<T>, (ReadBsonMessage) then this can be changed to T
                createSerializer().Serialize(bson, new MessageEnvelope<object> { Message = message });
            }

            observer.MessageWritten(compressedObservableStream.BytesWritten);
        }

        public T ReadMessage<T>(Stream stream)
        {
            if (stream is IRewindableBuffer rewindable)
            {
                return ReadCompressedMessageRewindable<T>(stream, rewindable);
            }

            return ReadCompressedMessage<T>(stream);
        }

        T ReadCompressedMessage<T>(Stream stream)
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
            using (var decompressedObservableStream = new ObservableStream(zip))
            using (var bson = new BsonDataReader(decompressedObservableStream) { CloseInput = false })
            {
                var messageEnvelope = DeserializeMessage<T>(bson);

                observer.MessageRead(decompressedObservableStream.BytesRead);

                return messageEnvelope.Message;
            }
        }

        T ReadCompressedMessageRewindable<T>(Stream stream, IRewindableBuffer rewindable)
        {
            rewindable.StartBuffer();
            try
            {
                using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
                using (var decompressedObservableStream = new ObservableStream(zip))
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

                    observer.MessageRead(decompressedObservableStream.BytesRead);

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
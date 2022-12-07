using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Transport.Protocol
{
    public class MessageSerializer : IMessageSerializer
    {
        readonly ITypeRegistry typeRegistry;
        readonly Func<JsonSerializer> createSerializer;
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
        }

        internal MessageSerializer(ITypeRegistry typeRegistry, Func<JsonSerializer> createSerializer)
        {
            this.typeRegistry = typeRegistry;
            this.createSerializer = createSerializer;
            deflateReflector = new DeflateStreamInputBufferReflector();
        }

        public void AddToMessageContract(params Type[] types) // kept for backwards compatibility
        {
            typeRegistry.AddToMessageContract(types);
        }

        public void TryTakeNote(Stream stream, string msg)
        {
            StreamAndRecord streamAndRecord = null;
            if (stream is RewindableBufferStream)
            {
                var rewindableBufferStream = (RewindableBufferStream) stream;
                TryTakeNote(rewindableBufferStream.baseStream, msg);
                
            }
            if (stream is StreamAndRecord)
            {
                streamAndRecord = (StreamAndRecord) stream;
                streamAndRecord.MakeNote(msg);
            }
        }
        
        public void WriteMessage<T>(Stream stream, T message)
        {
            TryTakeNote(stream, "\nSENDING ZIP\n");

            using (var zip = new DeflateStream(stream, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                // for the moment this MUST be object so that the $type property is included
                // If it is not, then an old receiver (eg, old tentacle) will not be able to understand messages from a new sender (server)
                // Once ALL sources and targets are deserializing to MessageEnvelope<T>, (ReadBsonMessage) then this can be changed to T
                createSerializer().Serialize(bson, new MessageEnvelope<object> { Message = message });
            }

            TryTakeNote(stream, "\nSENDING ZIP done\n");
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
            using (var bson = new BsonDataReader(zip) { CloseInput = false })
            {
                var messageEnvelope = DeserializeMessage<T>(bson);
                return messageEnvelope.Message;
            }
        }

        T ReadCompressedMessageRewindable<T>(Stream stream, IRewindableBuffer rewindable)
        {
            
            TryTakeNote(stream, "\nBEGIN read zip with rewind\n");

            rewindable.StartBuffer();
            try
            {
                using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
                using (var bson = new BsonDataReader(zip) { CloseInput = false })
                {
                    var messageEnvelope = DeserializeMessage<T>(bson);
                    
                    // Chance of a fix here:
                    var b = new byte[1024];
                    var res = zip.Read(b, 0, b.Length);
                    // Chance of fix END.

                    // Find the unused bytes in the DeflateStream input buffer
                    if (deflateReflector.TryGetAvailableInputBufferSize(zip, out var unusedBytesCount))
                    {
                        rewindable.FinishAndRewind(unusedBytesCount);
                        TryTakeNote(stream, $"\nbufferread rewind by {unusedBytesCount}\n");
                    }
                    else
                    {
                        rewindable.CancelBuffer();
                    }
                    return messageEnvelope.Message;
                }
            }
            catch
            {
                rewindable.CancelBuffer();
                throw;
            }
            finally
            {
                TryTakeNote(stream, "\nDONE read zip with rewind\n");
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
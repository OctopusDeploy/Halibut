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
        }

        internal MessageSerializer(ITypeRegistry typeRegistry, Func<JsonSerializer> createSerializer)
        {
            this.typeRegistry = typeRegistry;
            this.createSerializer = createSerializer;
        }

        public void AddToMessageContract(params Type[] types) // kept for backwards compatibility
        {
            typeRegistry.AddToMessageContract(types);
        }
        
        public void WriteMessage<T>(Stream stream, T message)
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                // for the moment this MUST be object so that the $type property is included
                // If it is not, then an old receiver (eg, old tentacle) will not be able to understand messages from a new sender (server)
                // Once ALL sources and targets are deserializing to MessageEnvelope<T>, (ReadBsonMessage) then this can be changed to T
                createSerializer().Serialize(bson, new MessageEnvelope<object> { Message = message });
            }
        }

        public T ReadMessage<T>(Stream stream)
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
            using (var bson = new BsonDataReader(zip) { CloseInput = false })
            {
                var messageEnvelope = createSerializer().Deserialize<MessageEnvelope<T>>(bson);
                if (messageEnvelope == null)
                    throw new Exception("messageEnvelope is null");
                
                return messageEnvelope.Message;
            }
        }
        
        // By making this a generic type, each message specifies the exact type it sends/expects
        // And it is impossible to deserialize the wrong type - any mismatched type will refuse to deserialize
        class MessageEnvelope<T>
        {
            public T Message { get; set; }
        }
    }
}
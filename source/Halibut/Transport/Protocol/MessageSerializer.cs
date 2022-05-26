using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Transport.Protocol
{
    public class MessageSerializer : IMessageSerializer
    {
        readonly RegisteredSerializationBinder binder = new RegisteredSerializationBinder();
        readonly HashSet<Type> messageContractTypes = new HashSet<Type>();
        
        JsonSerializer CreateSerializer()
        {
            var jsonSerializer = JsonSerializer.Create();
            jsonSerializer.Formatting = Formatting.None;
            jsonSerializer.ContractResolver = HalibutContractResolver.Instance;
            jsonSerializer.TypeNameHandling = TypeNameHandling.Auto;
            jsonSerializer.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;
            jsonSerializer.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            jsonSerializer.SerializationBinder = binder;
            return jsonSerializer;
        }

        public void AddToMessageContract(params Type[] types)
        {
            lock (messageContractTypes)
            {
                var newTypes = types.Where(t => messageContractTypes.Add(t)).ToArray();
                binder.Register(newTypes);
            }
        }
        
        public void WriteMessage<T>(Stream stream, T message)
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                // for the moment this MUST be object so that the $type property is included
                // If it is not, then an old receiver (eg, old tentacle) will not be able to understand messages from a new sender (server)
                // Once ALL sources and targets are deserializing to MessageEnvelope<T>, (ReadBsonMessage) then this can be changed to T
                CreateSerializer().Serialize(bson, new MessageEnvelope<object> { Message = message });
            }
        }

        public T ReadMessage<T>(Stream stream)
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
            using (var bson = new BsonDataReader(zip) { CloseInput = false })
            {
                var messageEnvelope = CreateSerializer().Deserialize<MessageEnvelope<T>>(bson);
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
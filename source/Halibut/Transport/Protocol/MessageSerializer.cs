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

        // NOTE: Do not share the serializer between Read/Write, the HalibutContractResolver adds OnSerializedCallbacks which are specific to the 
        // operation that is in-progress (and if the list is being enumerated at the time causes an exception).  And probably adds a duplicate each time the 
        // type is detected. It also makes use of a static, StreamCapture.Current - which seems like a bad™ idea, perhaps this can be straightened out? 
        // For now, just ensuring each operation does not interfere with each other.
        JsonSerializer CreateSerializer()
        {
            var jsonSerializer = JsonSerializer.Create();
            jsonSerializer.Formatting = Formatting.None;
            jsonSerializer.ContractResolver = new HalibutContractResolver();
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
            rewindable.StartRewindBuffer();
            try
            {
                using (var tracker = new ReadTrackerStream(stream))
                using (var zip = new Ionic.Zlib.DeflateStream(tracker, Ionic.Zlib.CompressionMode.Decompress, true))
                using (var bson = new BsonDataReader(zip) { CloseInput = false })
                {
                    var messageEnvelope = DeserializeMessage<T>(bson);

                    // 'zip.Position' represents the number of compressed bytes read.
                    // The difference between the total bytes read and the compressed bytes read will be
                    // the additional bytes over-read and consumed by the DeflateStream buffer.
                    rewindable.FinishRewindBuffer(tracker.TotalBytesRead - zip.Position);
                    return messageEnvelope.Message;
                }
            }
            catch
            {
                rewindable.CancelRewindBuffer();
                throw;
            }
        }

        MessageEnvelope<T> DeserializeMessage<T>(JsonReader reader)
        {
            var result = CreateSerializer().Deserialize<MessageEnvelope<T>>(reader);
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
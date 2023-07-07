using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Transport.Protocol
{
    public class MessageSerializer : IMessageSerializer
    {
        const long MemoryOverflowLimitBytes = 1024L * 1600L * 1000L;
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

        public async Task WriteMessageAsync<T>(Stream stream, T message)
        {
            using var compressedInMemoryBuffer = new FileOverflowMemoryStream(MemoryOverflowLimitBytes);
            using (var zip = new DeflateStream(compressedInMemoryBuffer, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                // for the moment this MUST be object so that the $type property is included
                // If it is not, then an old receiver (eg, old tentacle) will not be able to understand messages from a new sender (server)
                // Once ALL sources and targets are deserializing to MessageEnvelope<T>, (ReadBsonMessage) then this can be changed to T
                createSerializer().Serialize(bson, new MessageEnvelope<object> { Message = message });
            }

            compressedInMemoryBuffer.Position = 0;
            await compressedInMemoryBuffer.CopyToAsync(stream);
        }

        public T ReadMessage<T>(Stream stream)
        {
            if (stream is IRewindableBuffer rewindable)
            {
                return ReadCompressedMessageRewindable<T>(stream, rewindable);
            }

            return ReadCompressedMessage<T>(stream);
        }

        public async Task<T> ReadMessageAsync<T>(Stream stream)
        {
            if (stream is IRewindableBuffer rewindable)
            {
                //return await ReadCompressedMessageRewindableAsyncVanilla<T>(stream, rewindable);
                return await ReadCompressedMessageRewindableAsyncCpuOptimized<T>(stream, rewindable);
                //return await ReadCompressedMessageRewindableAsyncMemoryOptimized<T>(stream, rewindable);
            }

            return await ReadCompressedMessageAsync<T>(stream);
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

        async Task<T> ReadCompressedMessageAsync<T>(Stream stream)
        {
            using var zip = new DeflateStream(stream, CompressionMode.Decompress, true);

            using var deflatedInMemoryStream = new FileOverflowMemoryStream(MemoryOverflowLimitBytes);
            await zip.CopyToAsync(deflatedInMemoryStream);

            deflatedInMemoryStream.Position = 0;
            using (var bson = new BsonDataReader(deflatedInMemoryStream) { CloseInput = false })
            {
                var messageEnvelope = DeserializeMessage<T>(bson);
                return messageEnvelope.Message;
            }
        }

        T ReadCompressedMessageRewindable<T>(Stream stream, IRewindableBuffer rewindable)
        {
            rewindable.StartBuffer();
            try
            {
                using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
                using (var bson = new BsonDataReader(zip) { CloseInput = false })
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
                    return messageEnvelope.Message;
                }
            }
            catch
            {
                rewindable.CancelBuffer();
                throw;
            }
        }

        async Task<T> ReadCompressedMessageRewindableAsyncVanilla<T>(Stream stream, IRewindableBuffer rewindable)
        {
            await Task.CompletedTask;

            rewindable.StartBuffer();
            try
            {
                using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
                using (var bson = new BsonDataReader(zip) { CloseInput = false })
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
                    return messageEnvelope.Message;
                }
            }
            catch
            {
                rewindable.CancelBuffer();
                throw;
            }
        }

        async Task<T> ReadCompressedMessageRewindableAsyncMemoryOptimized<T>(Stream stream, IRewindableBuffer rewindable)
        {
            rewindable.StartBuffer();
            try
            {
                using var compressedInMemoryStream = new FileOverflowMemoryStream(MemoryOverflowLimitBytes);
                using var testerBufferStream = new CopyBytesReadToDestinationStream(stream, compressedInMemoryStream);
                using (var zip = new DeflateStream(testerBufferStream, CompressionMode.Decompress, true))
                {
                    var unimportantBuffer = new byte[1024 * 4];
                    while (await zip.ReadAsync(unimportantBuffer, 0, unimportantBuffer.Length) != 0)
                    {
                    }

                    // Find the unused bytes in the DeflateStream input buffer
                    if (deflateReflector.TryGetAvailableInputBufferSize(zip, out var unusedBytesCount))
                    {
                        rewindable.FinishAndRewind(unusedBytesCount);
                    }
                    else
                    {
                        rewindable.CancelBuffer();
                    }
                }

                compressedInMemoryStream.Position = 0;
                using (var zip = new DeflateStream(compressedInMemoryStream, CompressionMode.Decompress, true))
                using (var bson = new BsonDataReader(zip) { CloseInput = false })
                {
                    var messageEnvelope = DeserializeMessage<T>(bson);
                    
                    return messageEnvelope.Message;
                }
            }
            catch
            {
                rewindable.CancelBuffer();
                throw;
            }
        }

        async Task<T> ReadCompressedMessageRewindableAsyncCpuOptimized<T>(Stream stream, IRewindableBuffer rewindable)
        {
            rewindable.StartBuffer();
            try
            {
                using var zip = new DeflateStream(stream, CompressionMode.Decompress, true);

                using var deflatedInMemoryStream = new FileOverflowMemoryStream(MemoryOverflowLimitBytes);
                await zip.CopyToAsync(deflatedInMemoryStream);

                // Find the unused bytes in the DeflateStream input buffer
                if (deflateReflector.TryGetAvailableInputBufferSize(zip, out var unusedBytesCount))
                {
                    rewindable.FinishAndRewind(unusedBytesCount);
                }
                else
                {
                    rewindable.CancelBuffer();
                }

                deflatedInMemoryStream.Position = 0;
                using (var bson = new BsonDataReader(deflatedInMemoryStream) { CloseInput = false })
                {
                    var messageEnvelope = DeserializeMessage<T>(bson);

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
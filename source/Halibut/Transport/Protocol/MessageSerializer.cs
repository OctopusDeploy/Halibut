using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
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
        readonly long readIntoMemoryLimitBytes;

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
            IMessageSerializerObserver observer,
            long readIntoMemoryLimitBytes)
        {
            this.typeRegistry = typeRegistry;
            this.createSerializer = createSerializer;
            this.observer = observer;
            this.readIntoMemoryLimitBytes = readIntoMemoryLimitBytes;
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

        public async Task WriteMessageAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
        {
            using var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen);
            using var compressedInMemoryBuffer = new WriteAsyncIfPossibleStream(compressedByteCountingStream, readIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);
            using (var zip = new DeflateStream(compressedInMemoryBuffer, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                // for the moment this MUST be object so that the $type property is included
                // If it is not, then an old receiver (eg, old tentacle) will not be able to understand messages from a new sender (server)
                // Once ALL sources and targets are deserializing to MessageEnvelope<T>, (ReadBsonMessage) then this can be changed to T
                createSerializer().Serialize(bson, new MessageEnvelope<object> { Message = message });
            }

            await compressedInMemoryBuffer.WriteAnyUnwrittenDataToWrappedStream(cancellationToken);
            
            observer.MessageWritten(compressedByteCountingStream.BytesWritten);
        }

        public T ReadMessage<T>(Stream stream)
        {
            var messageReader = MessageReaderStrategyFromStream<T>(stream);
            using (var errorRecordingStream = new ErrorRecordingStream(stream, closeInner: false))
            {
                Exception exceptionFromDeserialisation = null;
                try
                {
                    return messageReader(errorRecordingStream);
                }
                catch (Exception e)
                {
                    exceptionFromDeserialisation = e;
                }
                finally
                {
                    if (errorRecordingStream.ReadExceptions.Count == 1)
                    {
                        throw errorRecordingStream.ReadExceptions[0];
                    }

                    if (errorRecordingStream.WasTheEndOfStreamEncountered)
                    {
                        throw new EndOfStreamException();
                    }

                    if (errorRecordingStream.ReadExceptions.Count > 0)
                    {
                        throw new IOException("Error Reading from stream", new AggregateException(errorRecordingStream.ReadExceptions));
                    }
                }
                
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

        public async Task<T> ReadMessageAsync<T>(Stream stream, CancellationToken cancellationToken)
        {
            if (stream is IRewindableBuffer rewindable)
            {
                return await ReadCompressedMessageRewindableAsyncCpuOptimized<T>(stream, rewindable, cancellationToken);
                //return await ReadCompressedMessageRewindableAsyncMemoryOptimized<T>(stream, rewindable);
            }

            return await ReadCompressedMessageAsync<T>(stream, cancellationToken);
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

        async Task<T> ReadCompressedMessageAsync<T>(Stream stream, CancellationToken cancellationToken)
        {
            using var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen);
            using var zip = new DeflateStream(compressedByteCountingStream, CompressionMode.Decompress, true);
            using var decompressedByteCountingStream = new ByteCountingStream(zip, OnDispose.LeaveInputStreamOpen);

            using var deflatedInMemoryStream = new ReadAsyncIfPossibleStream(decompressedByteCountingStream, readIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);
            await deflatedInMemoryStream.BufferFromSourceStreamUntilLimitReached(cancellationToken);
            
            using (var bson = new BsonDataReader(deflatedInMemoryStream) { CloseInput = false })
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
        
        Task<T> ReadCompressedMessageRewindableAsyncMemoryOptimized<T>(Stream stream, IRewindableBuffer rewindable)
        {
            //TODO: We would have to create another stream type to get this to work. But is it worth spiking now?
            throw new NotImplementedException();
            //rewindable.StartBuffer();
            //try
            //{
            //    using var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen);
            //    using var compressedInMemoryStream = new FileOverflowMemoryStream(readIntoMemoryLimitBytes);
            //    using var testerBufferStream = new CopyBytesReadToDestinationStream(stream, compressedInMemoryStream);
            //    using (var zip = new DeflateStream(testerBufferStream, CompressionMode.Decompress, true))
            //    {
            //        var unimportantBuffer = new byte[1024 * 4];
            //        while (await zip.ReadAsync(unimportantBuffer, 0, unimportantBuffer.Length) != 0)
            //        {
            //        }

            //        // Find the unused bytes in the DeflateStream input buffer
            //        if (deflateReflector.TryGetAvailableInputBufferSize(zip, out var unusedBytesCount))
            //        {
            //            rewindable.FinishAndRewind(unusedBytesCount);
            //        }
            //        else
            //        {
            //            rewindable.CancelBuffer();
            //        }
            //    }

            //    compressedInMemoryStream.Position = 0;
            //    using (var zip = new DeflateStream(compressedInMemoryStream, CompressionMode.Decompress, true))
            //    using (var bson = new BsonDataReader(zip) { CloseInput = false })
            //    {
            //        var messageEnvelope = DeserializeMessage<T>(bson);
                    
            //        return messageEnvelope.Message;
            //    }
            //}
            //catch
            //{
            //    rewindable.CancelBuffer();
            //    throw;
            //}
        }

        async Task<T> ReadCompressedMessageRewindableAsyncCpuOptimized<T>(Stream stream, IRewindableBuffer rewindable, CancellationToken cancellationToken)
        {
            rewindable.StartBuffer();
            try
            {
                using var zip = new DeflateStream(stream, CompressionMode.Decompress, true);

                using var deflatedInMemoryStream = new ReadAsyncIfPossibleStream(zip, readIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);
                await deflatedInMemoryStream.BufferFromSourceStreamUntilLimitReached(cancellationToken);

                // Find the unused bytes in the DeflateStream input buffer
                if (deflateReflector.TryGetAvailableInputBufferSize(zip, out var unusedBytesCount))
                {
                    rewindable.FinishAndRewind(unusedBytesCount);
                }
                else
                {
                    rewindable.CancelBuffer();
                }
                
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
using System;
using System.IO;
using System.IO.Compression;
using Halibut.Diagnostics;
using Halibut.Transport.Observability;
using Halibut.Transport.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System.Threading.Tasks;
using System.Threading;

namespace Halibut.Transport.Protocol
{
    public class MessageSerializer : IMessageSerializer
    {
        readonly ITypeRegistry typeRegistry;
        readonly Func<JsonSerializer> createSerializer;
        readonly IMessageSerializerObserver observer;
        readonly long readIntoMemoryLimitBytes;
        readonly long writeIntoMemoryLimitBytes;
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
            deflateReflector = new DeflateStreamInputBufferReflector(new InMemoryConnectionLog("poll://foo/"));
            observer = new NoMessageSerializerObserver();
        }

        internal MessageSerializer(
            ITypeRegistry typeRegistry, 
            Func<JsonSerializer> createSerializer,
            IMessageSerializerObserver observer,
            long readIntoMemoryLimitBytes,
            long writeIntoMemoryLimitBytes)
        {
            this.typeRegistry = typeRegistry;
            this.createSerializer = createSerializer;
            this.observer = observer;
            this.readIntoMemoryLimitBytes = readIntoMemoryLimitBytes;
            this.writeIntoMemoryLimitBytes = writeIntoMemoryLimitBytes;
            deflateReflector = new DeflateStreamInputBufferReflector(new InMemoryConnectionLog("poll://foo/"));
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
            using var compressedInMemoryBuffer = new WriteIntoMemoryBufferStream(compressedByteCountingStream, writeIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);

            using (var zip = new DeflateStream(compressedInMemoryBuffer, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                // for the moment this MUST be object so that the $type property is included
                // If it is not, then an old receiver (eg, old tentacle) will not be able to understand messages from a new sender (server)
                // Once ALL sources and targets are deserializing to MessageEnvelope<T>, (ReadBsonMessage) then this can be changed to T
                createSerializer().Serialize(bson, new MessageEnvelope<object> { Message = message });
            }

            await compressedInMemoryBuffer.WriteAnyUnwrittenDataToSinkStream(cancellationToken);

            observer.MessageWritten(compressedByteCountingStream.BytesWritten);
        }

        public T ReadMessage<T>(RewindableBufferStream stream)
        {
            using (var errorRecordingStream = new ErrorRecordingStream(stream, closeInner: false))
            {
                Exception exceptionFromDeserialisation = null;
                try
                {
                    return ReadCompressedMessage<T>(errorRecordingStream, stream);
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

        public async Task<T> ReadMessageAsync<T>(RewindableBufferStream stream, CancellationToken cancellationToken)
        {
            using (var errorRecordingStream = new ErrorRecordingStream(stream, closeInner: false))
            {
                Exception exceptionFromDeserialisation = null;
                try
                {
                    return await ReadCompressedMessageAsync<T>(errorRecordingStream, stream);
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
        
        T ReadCompressedMessage<T>(Stream stream, IRewindableBuffer rewindableBuffer)
        {
            rewindableBuffer.StartBuffer();
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
                        rewindableBuffer.FinishAndRewind(unusedBytesCount);
                    }
                    else
                    {
                        rewindableBuffer.CancelBuffer();
                    }

                    observer.MessageRead(compressedByteCountingStream.BytesRead - unusedBytesCount, decompressedObservableStream.BytesRead);

                    return messageEnvelope.Message;
                }
            }
            catch
            {
                rewindableBuffer.CancelBuffer();
                throw;
            }
        }
        
        async Task<T> ReadCompressedMessageAsync<T>(Stream stream, IRewindableBuffer rewindableBuffer)
        {
            rewindableBuffer.StartBuffer();
            try
            {
                using var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen);
                using var zip = new DeflateStream(compressedByteCountingStream, CompressionMode.Decompress, true);
                using var decompressedObservableStream = new ByteCountingStream(zip, OnDispose.LeaveInputStreamOpen);

                using var deflatedInMemoryStream = new ReadAsyncIfPossibleStream(decompressedByteCountingStream, readIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);
                await deflatedInMemoryStream.BufferFromSourceStreamUntilLimitReached(cancellationToken);

                using (var bson = new BsonDataReader(deflatedInMemoryStream) { CloseInput = false })
                {
                    var messageEnvelope = DeserializeMessage<T>(bson);

                    // Find the unused bytes in the DeflateStream input buffer
                    if (deflateReflector.TryGetAvailableInputBufferSize(zip, out var unusedBytesCount))
                    {
                        rewindableBuffer.FinishAndRewind(unusedBytesCount);
                    }
                    else
                    {
                        rewindableBuffer.CancelBuffer();
                    }

                    observer.MessageRead(compressedByteCountingStream.BytesRead - unusedBytesCount, decompressedObservableStream.BytesRead);
                    return messageEnvelope.Message;
                }
            }
            catch
            {
                rewindableBuffer.CancelBuffer();
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
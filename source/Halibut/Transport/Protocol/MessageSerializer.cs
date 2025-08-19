using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Observability;
using Halibut.Transport.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Transport.Protocol
{
    public class MessageSerializer : IMessageSerializer
    {
        readonly Func<StreamCapturingJsonSerializer> createStreamCapturingSerializer;
        readonly IMessageSerializerObserver observer;
        readonly long readIntoMemoryLimitBytes;
        readonly long writeIntoMemoryLimitBytes;
        readonly DeflateStreamInputBufferReflector deflateReflector;
        
        internal MessageSerializer(
            ITypeRegistry typeRegistry,
            Func<StreamCapturingJsonSerializer> createStreamCapturingSerializer,
            IMessageSerializerObserver observer,
            long readIntoMemoryLimitBytes,
            long writeIntoMemoryLimitBytes,
            ILogFactory logFactory)
        {
            this.createStreamCapturingSerializer = createStreamCapturingSerializer;
            this.observer = observer;
            this.readIntoMemoryLimitBytes = readIntoMemoryLimitBytes;
            this.writeIntoMemoryLimitBytes = writeIntoMemoryLimitBytes;
            deflateReflector = new DeflateStreamInputBufferReflector(logFactory.ForPrefix(nameof(MessageSerializer)));
        }

        public async Task<IReadOnlyList<DataStream>> WriteMessageAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
        {
            IReadOnlyList<DataStream> serializedStreams;

            using var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen);
            using var compressedInMemoryBuffer = new WriteIntoMemoryBufferStream(compressedByteCountingStream, writeIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);

#if !NETFRAMEWORK
            await
#endif
            using (var zip = new DeflateStream(compressedInMemoryBuffer, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                // for the moment this MUST be object so that the $type property is included
                // If it is not, then an old receiver (eg, old tentacle) will not be able to understand messages from a new sender (server)
                // Once ALL sources and targets are deserializing to MessageEnvelope<T>, (ReadBsonMessage) then this can be changed to T
                var streamCapturingSerializer = createStreamCapturingSerializer();
                streamCapturingSerializer.Serializer.Serialize(bson, new MessageEnvelope<object> { Message = message! });

                serializedStreams = streamCapturingSerializer.DataStreams;
            }

            await compressedInMemoryBuffer.WriteBufferToUnderlyingStream(cancellationToken);

            observer.MessageWritten(compressedByteCountingStream.BytesWritten, compressedInMemoryBuffer.BytesWrittenIntoMemory);

            return serializedStreams;
        }

        public async Task<(T Message, IReadOnlyList<DataStream> DataStreams)> ReadMessageAsync<T>(RewindableBufferStream stream, CancellationToken cancellationToken)
        {
            await using (var errorRecordingStream = new ErrorRecordingStream(stream, closeInner: false))
            {
                Exception? exceptionFromDeserialisation = null;
                try
                {
                    return await ReadCompressedMessageAsync<T>(errorRecordingStream, stream, cancellationToken);
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

        async Task<(T Message, IReadOnlyList<DataStream> DataStreams)> ReadCompressedMessageAsync<T>(ErrorRecordingStream stream, IRewindableBuffer rewindableBuffer, CancellationToken cancellationToken)
        {
            rewindableBuffer.StartBuffer();
            try
            {
                await using var compressedByteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen);
                
#if !NETFRAMEWORK
                await
#endif
                using var zip = new DeflateStream(compressedByteCountingStream, CompressionMode.Decompress, true);
                using var decompressedByteCountingStream = new ByteCountingStream(zip, OnDispose.LeaveInputStreamOpen);

                await using var deflatedInMemoryStream = new ReadIntoMemoryBufferStream(decompressedByteCountingStream, readIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);
                await deflatedInMemoryStream.BufferIntoMemoryFromSourceStreamUntilLimitReached(cancellationToken);
                
                // If the end of stream was found and we read nothing from the streams
                if (stream.WasTheEndOfStreamEncountered && compressedByteCountingStream.BytesRead == 0 && decompressedByteCountingStream.BytesRead == 0)
                {
                    // When this happens we would normally continue to the BsonDataReader which would
                    // do a sync read(), to find that the stream had ended. We avoid that sync call by
                    // short circuiting to what would happen which is:  
                    // The BsonReader would return a non null MessageEnvelope with a null message, which is what we do here.
                    return (new MessageEnvelope<T>().Message, Array.Empty<DataStream>()); // And hack around we can't return null
                }

                using (var bson = new BsonDataReader(deflatedInMemoryStream) { CloseInput = false })
                {
                    var (messageEnvelope, dataStreams) = DeserializeMessageAndDataStreams<T>(bson);

                    // Find the unused bytes in the DeflateStream input buffer
                    if (deflateReflector.TryGetAvailableInputBufferSize(zip, out var unusedBytesCount))
                    {
                        rewindableBuffer.FinishAndRewind(unusedBytesCount);
                    }
                    else
                    {
                        rewindableBuffer.CancelBuffer();
                    }

                    observer.MessageRead(compressedByteCountingStream.BytesRead - unusedBytesCount, decompressedByteCountingStream.BytesRead, deflatedInMemoryStream.BytesReadIntoMemory);
                    return (messageEnvelope.Message, dataStreams);
                }
            }
            catch
            {
                rewindableBuffer.CancelBuffer();
                throw;
            }
        }

        (MessageEnvelope<T> MessageEnvelope, IReadOnlyList<DataStream> DataStreams) DeserializeMessageAndDataStreams<T>(JsonReader reader)
        {
            var streamCapturingSerializer = createStreamCapturingSerializer();
            var result = streamCapturingSerializer.Serializer.Deserialize<MessageEnvelope<T>>(reader);
            
            if (result == null)
            {
                throw new Exception("messageEnvelope is null");
            }

            return (result, streamCapturingSerializer.DataStreams);
        }
        
        // By making this a generic type, each message specifies the exact type it sends/expects
        // And it is impossible to deserialize the wrong type - any mismatched type will refuse to deserialize
        class MessageEnvelope<T>
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            public T Message { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        }
    }
}

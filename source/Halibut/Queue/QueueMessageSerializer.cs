
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Halibut.Queue.MessageStreamWrapping;
using Halibut.Transport.Protocol;
using Halibut.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Queue
{
    /// <summary>
    /// Uses the same JSON serializer used by Halibut to send messages over the wire to
    /// serialise messages for the queue. Note that the queue serialises to JSON rather
    /// than BSON which is what is sent over the wire.
    ///
    /// Based on battle-tested MessageSerializer, any quirks may be inherited from there.
    /// </summary>
    public class QueueMessageSerializer
    {
        readonly Func<StreamCapturingJsonSerializer> createStreamCapturingSerializer;
        readonly MessageStreamWrappers messageStreamWrappers;

        public QueueMessageSerializer(Func<StreamCapturingJsonSerializer> createStreamCapturingSerializer, MessageStreamWrappers messageStreamWrappers)
        {
            this.createStreamCapturingSerializer = createStreamCapturingSerializer;
            this.messageStreamWrappers = messageStreamWrappers;
        }
        
        public async Task<(byte[], IReadOnlyList<DataStream>)> PrepareMessageForWireTransferAndForQueue<T>(T message)
        {
            IReadOnlyList<DataStream> dataStreams;
            
            using var ms = new MemoryStream();
            Stream stream = ms;
            await using (var wrappedStreamDisposables = new DisposableCollection())
            {
                stream = WrapInMessageSerialisationStreams(messageStreamWrappers, stream, wrappedStreamDisposables);

                // TODO instead store 
                using (var zip = new DeflateStream(stream, CompressionMode.Compress, true)) {
                    using (var jsonTextWriter = new BsonDataWriter(zip) { CloseOutput = false })
                    {
                        var streamCapturingSerializer = createStreamCapturingSerializer();
                        streamCapturingSerializer.Serializer.Serialize(jsonTextWriter, new MessageEnvelope<T>(message));
                        dataStreams = streamCapturingSerializer.DataStreams;
                    }
                }
            }

            return (ms.ToArray(), dataStreams);
        }
        
        public async Task<byte[]> ReadBytesForWireTransfer(byte[] dataStoredInRedis)
        {
            using var ms = new MemoryStream(dataStoredInRedis);
            Stream stream = ms;
            await using var disposables = new DisposableCollection();
            stream = WrapStreamInMessageDeserialisationStreams(messageStreamWrappers, stream, disposables);
            
            using var output = new MemoryStream();
            
            stream.CopyTo(output);

            return output.ToArray();
        }
        

        public async Task<(byte[], IReadOnlyList<DataStream>)> WriteMessage<T>(T message)
        {
            IReadOnlyList<DataStream> dataStreams;
            
            using var ms = new MemoryStream();
            Stream stream = ms;
            await using (var wrappedStreamDisposables = new DisposableCollection())
            {
                stream = WrapInMessageSerialisationStreams(messageStreamWrappers, stream, wrappedStreamDisposables);

                using (var sw = new StreamWriter(stream, Encoding.UTF8
#if NET8_0_OR_GREATER
                           , leaveOpen: true
#endif
                       ))
                {
                    using (var jsonTextWriter = new JsonTextWriter(sw) { CloseOutput = false })
                    {
                        var streamCapturingSerializer = createStreamCapturingSerializer();
                        streamCapturingSerializer.Serializer.Serialize(jsonTextWriter, new MessageEnvelope<T>(message));
                        dataStreams = streamCapturingSerializer.DataStreams;
                    }
                }
            }

            return (ms.ToArray(), dataStreams);
        }

        public static Stream WrapInMessageSerialisationStreams(MessageStreamWrappers messageStreamWrappers, Stream stream, DisposableCollection disposables)
        {
            foreach (var streamer in messageStreamWrappers.Wrappers)
            {
                var wrappedStream = streamer.WrapMessageSerialisationStream(stream);
                if (!ReferenceEquals(wrappedStream, stream))
                {
                    stream = wrappedStream;
                    disposables.Add(stream);
                }
            }

            return stream;
        }

        public async Task<(T Message, IReadOnlyList<DataStream> DataStreams)> ReadMessage<T>(byte[] json)
        {
            using var ms = new MemoryStream(json);
            Stream stream = ms;
            await using var disposables = new DisposableCollection();
            stream = WrapStreamInMessageDeserialisationStreams(messageStreamWrappers, stream, disposables);
            using var sr = new StreamReader(stream, Encoding.UTF8
#if NET8_0_OR_GREATER
                       , leaveOpen: true
#endif
            );
            using var reader = new JsonTextReader(sr);
            var streamCapturingSerializer = createStreamCapturingSerializer();
            var result = streamCapturingSerializer.Serializer.Deserialize<MessageEnvelope<T>>(reader);
            
            if (result == null)
            {
                throw new Exception("messageEnvelope is null");
            }

            return (result.Message, streamCapturingSerializer.DataStreams);
        }

        public static Stream WrapStreamInMessageDeserialisationStreams(MessageStreamWrappers messageStreamWrappers, Stream stream, DisposableCollection disposables)
        {
            foreach (var streamer in messageStreamWrappers.Wrappers)
            {
                var wrappedStream = streamer.WrapMessageDeserialisationStream(stream);
                if (!ReferenceEquals(wrappedStream, stream))
                {
                    stream = wrappedStream;
                    disposables.Add(stream);
                }
            }

            return stream;
        }

        // By making this a generic type, each message specifies the exact type it sends/expects
        // And it is impossible to deserialize the wrong type - any mismatched type will refuse to deserialize
        class MessageEnvelope<T>
        {
            public MessageEnvelope(T message)
            {
                Message = message;
            }

            public T Message { get; private set; }
        }

        public async Task<byte[]> PrepareBytesFromWire(byte[] responseBytes)
        {
            using var inputStream = new MemoryStream(responseBytes);
            using var outputStream = new MemoryStream();
            
            Stream wrappedStream = outputStream;
            await using var disposables = new DisposableCollection();
            wrappedStream = WrapInMessageSerialisationStreams(messageStreamWrappers, wrappedStream, disposables);
            
            await inputStream.CopyToAsync(wrappedStream);
            await wrappedStream.FlushAsync();
            
            return outputStream.ToArray();
        }

        public async Task<(T response, IReadOnlyList<DataStream> dataStreams)> ConvertStoredResponseToResponseMessage<T>(byte[] storedMessageMessage)
        {
            using var ms = new MemoryStream(storedMessageMessage);
            Stream stream = ms;
            await using var disposables = new DisposableCollection();
            stream = WrapStreamInMessageDeserialisationStreams(messageStreamWrappers, stream, disposables);
            
            using var deflateStream = new DeflateStream(stream, CompressionMode.Decompress, true);
            using (var bson = new BsonDataReader(deflateStream) { CloseInput = false })
            {
                var streamCapturingSerializer = createStreamCapturingSerializer();
                var result = streamCapturingSerializer.Serializer.Deserialize<MessageEnvelope<T>>(bson);
            
                if (result == null)
                {
                    throw new Exception("messageEnvelope is null");
                }

                return (result.Message, streamCapturingSerializer.DataStreams);
            }
            
        }
    }
}
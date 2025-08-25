using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Halibut.Transport.Protocol;
using Newtonsoft.Json;

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

        public QueueMessageSerializer(Func<StreamCapturingJsonSerializer> createStreamCapturingSerializer)
        {
            this.createStreamCapturingSerializer = createStreamCapturingSerializer;
        }

        public (byte[], IReadOnlyList<DataStream>) WriteMessage<T>(T message)
        {
            IReadOnlyList<DataStream> dataStreams;
            
            using var ms = new MemoryStream();
            using (var sw = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true)){
                using (var jsonTextWriter = new JsonTextWriter(sw) { CloseOutput = false })
                {
                    var streamCapturingSerializer = createStreamCapturingSerializer();
                    streamCapturingSerializer.Serializer.Serialize(jsonTextWriter, new MessageEnvelope<T>(message));
                    dataStreams = streamCapturingSerializer.DataStreams;
                }
            }

            return (ms.ToArray(), dataStreams);
        }

        public (T Message, IReadOnlyList<DataStream> DataStreams) ReadMessage<T>(byte[] json)
        {
            using var ms = new MemoryStream(json);
            using var sr = new StreamReader(ms, Encoding.UTF8);
            using var reader = new JsonTextReader(sr);
            var streamCapturingSerializer = createStreamCapturingSerializer();
            var result = streamCapturingSerializer.Serializer.Deserialize<MessageEnvelope<T>>(reader);
            
            if (result == null)
            {
                throw new Exception("messageEnvelope is null");
            }

            return (result.Message, streamCapturingSerializer.DataStreams);
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
    }
}
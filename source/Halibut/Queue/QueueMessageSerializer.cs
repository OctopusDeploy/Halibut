using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Halibut.Transport.Protocol;
using Newtonsoft.Json;

namespace Halibut.Queue
{
    // TODO make an  interface
    public class QueueMessageSerializer
    {
        readonly Func<StreamCapturingJsonSerializer> createStreamCapturingSerializer;

        public QueueMessageSerializer(Func<StreamCapturingJsonSerializer> createStreamCapturingSerializer)
        {
            this.createStreamCapturingSerializer = createStreamCapturingSerializer;
        }

        public (string, IReadOnlyList<DataStream>) WriteMessage<T>(T message)
        {
            IReadOnlyList<DataStream> dataStreams;
            
            var sb = new StringBuilder();
            using var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
            using (var jsonTextWriter = new JsonTextWriter(sw) { CloseOutput = false })
            {
                var streamCapturingSerializer = createStreamCapturingSerializer();
                streamCapturingSerializer.Serializer.Serialize(jsonTextWriter, new MessageEnvelope<object> { Message = message! });
                dataStreams = streamCapturingSerializer.DataStreams;
            }

            return (sb.ToString(), dataStreams);
        }

        public (T Message, IReadOnlyList<DataStream> DataStreams) ReadMessage<T>(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json));
            {
                
                var streamCapturingSerializer = createStreamCapturingSerializer();
                var result = streamCapturingSerializer.Serializer.Deserialize<MessageEnvelope<T>>(reader);
            
                if (result == null)
                {
                    throw new Exception("messageEnvelope is null");
                }

                return (result.Message, streamCapturingSerializer.DataStreams);
            }
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
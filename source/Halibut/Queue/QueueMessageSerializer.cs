// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Halibut.Transport.Protocol;
using Newtonsoft.Json;

namespace Halibut.Queue
{
    public class QueueMessageSerializer
    {
        readonly Func<StreamCapturingJsonSerializer> createStreamCapturingSerializer;

        public QueueMessageSerializer(Func<StreamCapturingJsonSerializer> createStreamCapturingSerializer)
        {
            this.createStreamCapturingSerializer = createStreamCapturingSerializer;
        }

        public (string, IReadOnlyList<DataStream>) WriteMessage<T>(T message)
        {
            IReadOnlyList<DataStream> datatStreams;
            
            var sb = new StringBuilder();
            using var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
            using (var jsonTextWriter = new JsonTextWriter(sw) { CloseOutput = false })
            {
                var streamCapturingSerializer = createStreamCapturingSerializer();
                streamCapturingSerializer.Serializer.Serialize(jsonTextWriter, new MessageEnvelope<object> { Message = message! });
                datatStreams = streamCapturingSerializer.DataStreams;
            }

            return (sb.ToString(), datatStreams);
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
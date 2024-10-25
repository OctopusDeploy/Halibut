using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class StreamCapturingJsonSerializer
    {
        readonly StreamCaptureContext streamCaptureContext;
        public JsonSerializer Serializer { get; }
        public IReadOnlyList<DataStream> DataStreams => streamCaptureContext.CapturedStreams;

        public StreamCapturingJsonSerializer(JsonSerializerSettings settings)
        {
            Serializer = JsonSerializer.Create(settings);
            streamCaptureContext = new StreamCaptureContext();
#pragma warning disable SYSLIB0050
            Serializer.Context = new StreamingContext(default, streamCaptureContext);
#pragma warning restore SYSLIB0050
        }

        public class StreamCaptureContext
        {
            readonly List<DataStream> streams = new();
            public IReadOnlyList<DataStream> CapturedStreams => streams;

            public void AddCapturedStream(DataStream stream)
            {
                streams.Add(stream);
            }
        }
    }
}
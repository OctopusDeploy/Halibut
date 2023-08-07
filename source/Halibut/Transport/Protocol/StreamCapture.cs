using System;
using System.Collections.Generic;
using System.Threading;

namespace Halibut.Transport.Protocol
{
    public class StreamCapture : IDisposable
    {
        readonly HashSet<DataStream> serializedStreams = new();
        readonly HashSet<DataStream> deserializedStreams = new();

        static AsyncLocal<StreamCapture> current = new();

        public static StreamCapture Current
        {
            get => current.Value;
            private set => current.Value = value;
        }

        public ICollection<DataStream> SerializedStreams => serializedStreams;

        public ICollection<DataStream> DeserializedStreams => deserializedStreams;

        public static StreamCapture New()
        {
            var capture = new StreamCapture();
            Current = capture;
            return capture;
        }

        public void Dispose()
        {
            Current = null;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using Halibut.Protocol;

namespace Halibut.Services
{
    public class StreamCapture : IDisposable
    {
        readonly HashSet<DataStream> serializedStreams = new HashSet<DataStream>();
        readonly HashSet<DataStream> deserializedStreams = new HashSet<DataStream>();

        public static StreamCapture Current
        {
            get { return (StreamCapture)CallContext.GetData("HalibutStreamCapture"); }
            private set { CallContext.SetData("HalibutStreamCapture", value); }
        }

        public ICollection<DataStream> SerializedStreams
        {
            get { return serializedStreams; }
        }

        public ICollection<DataStream> DeserializedStreams
        {
            get { return deserializedStreams; }
        }

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
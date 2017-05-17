using System;
using System.Collections.Generic;
using System.Threading;
using Janitor;

#if HAS_ASYNC_LOCAL
#else
using System.Runtime.Remoting.Messaging;
#endif

namespace Halibut.Transport.Protocol
{
    [SkipWeaving]
    public class StreamCapture : IDisposable
    {
        readonly HashSet<DataStream> serializedStreams = new HashSet<DataStream>();
        readonly HashSet<DataStream> deserializedStreams = new HashSet<DataStream>();

#if HAS_ASYNC_LOCAL
        static AsyncLocal<StreamCapture> current = new AsyncLocal<StreamCapture>();

        public static StreamCapture Current
        {
            get { return current.Value; }
            private set { current.Value = value; }
        }
#else
        public static StreamCapture Current
        {
            get { return (StreamCapture) CallContext.GetData("HalibutStreamCapture"); }
            private set { CallContext.SetData("HalibutStreamCapture", value); }
        }
#endif

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
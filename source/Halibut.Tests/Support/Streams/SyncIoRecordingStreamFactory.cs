using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;
using Halibut.Util;

namespace Halibut.Tests.Support.Streams
{
    public class SyncIoRecordingStreamFactory : IStreamFactory
    {
        public readonly List<SyncIoRecordingStream> streams = new();
        AsyncHalibutFeature asyncHalibutFeature;

        public SyncIoRecordingStreamFactory(AsyncHalibutFeature asyncHalibutFeature)
        {
            this.asyncHalibutFeature = asyncHalibutFeature;
        }

        public List<StackTrace> PlacesSyncIoWasUsed()
        {
            var stackTraces = new List<StackTrace>();
            foreach (var noSyncIoStream in streams)
            {
                stackTraces.AddRange(noSyncIoStream.SyncCalls);
            }

            return stackTraces;
        }

        public Stream CreateStream(TcpClient stream)
        {
            var s = new SyncIoRecordingStream(new StreamFactory(asyncHalibutFeature).CreateStream(stream));
            lock (streams)
            {
                streams.Add(s);
            }
            return s;
        }

        public WebSocketStream CreateStream(WebSocket webSocket)
        {
            throw new NotImplementedException();
        }
    }
}
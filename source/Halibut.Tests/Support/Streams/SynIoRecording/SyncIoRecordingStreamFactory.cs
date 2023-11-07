using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;

namespace Halibut.Tests.Support.Streams.SynIoRecording
{
    public class SyncIoRecordingStreamFactory : IStreamFactory
    {
        public readonly List<IRecordSyncIo> streams = new();
        
        public List<StackTrace> PlacesSyncIoWasUsed()
        {
            lock (streams)
            {
                var stackTraces = new List<StackTrace>();
                foreach (var noSyncIoStream in streams) stackTraces.AddRange(noSyncIoStream.SyncCalls);

                return stackTraces;
            }
        }

        void AddRecorder(IRecordSyncIo s)
        {
            lock (streams)
            {
                streams.Add(s);
            }
        }

        public Stream CreateStream(TcpClient stream)
        {
            var s = new SyncIoRecordingStream(new StreamFactory().CreateStream(stream));
            AddRecorder(s);
            return s;
        }

        public WebSocketStream CreateStream(WebSocket webSocket)
        {
            var wss = new SyncIoRecordingWebSocketStream(webSocket);
            AddRecorder(wss);
            return wss;
        }
    }
}
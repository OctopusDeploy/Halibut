using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Support.Streams.SynIoRecording
{
    public class SyncIoRecordingWebSocketStream : WebSocketStream, IRecordSyncIo
    {
        readonly List<StackTrace> syncCalls = new();
        
        public SyncIoRecordingWebSocketStream(WebSocket context) : base(context)
        {
        }

        
        public IReadOnlyList<StackTrace> SyncCalls
        {
            get
            {
                lock (syncCalls)
                {
                    // return a copy to avoid races against NoteSyncCall
                    return syncCalls.ToArray();
                }
            }
        }


        public void NoteSyncCall()
        {
            lock (syncCalls)
            {
                syncCalls.Add(new StackTrace());
            }
        }

        public override void Flush()
        {
            NoteSyncCall();
            base.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            NoteSyncCall();
            return base.Read(buffer, offset, count);
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            NoteSyncCall();
            base.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            NoteSyncCall();
            base.Dispose(disposing);
        }
    }
}
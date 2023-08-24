using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Support.Streams.SynIoRecording
{
    public class SyncIoRecordingWebSocketStream : WebSocketStream, IRecordSyncIo
    {
        
        public SyncIoRecordingWebSocketStream(WebSocket context) : base(context)
        {
        }

        public List<StackTrace> SyncCalls { get; } = new();

        public void NoteSyncCall()
        {
            lock (SyncCalls)
            {
                SyncCalls.Add(new StackTrace());
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

        
        public async Task<string> ReadTextMessageSynchronouslyAsync()
        {
            NoteSyncCall();
            return await base.ReadTextMessageSynchronouslyAsync();
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
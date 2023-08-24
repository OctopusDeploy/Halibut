using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Halibut.Transport.Streams;
#if NETFRAMEWORK
#endif

namespace Halibut.Tests.Support.Streams.SynIoRecording
{
    public class SyncIoRecordingStream : DelegateStreamBase, IRecordSyncIo
#if NETFRAMEWORK
        , IAsyncDisposable
#endif
    {
        public SyncIoRecordingStream(Stream inner)
        {
            Inner = inner;
        }

        public List<StackTrace> SyncCalls { get; } = new();

        public void NoteSyncCall()
        {
            lock (SyncCalls)
            {
                SyncCalls.Add(new StackTrace());
            }
        }

        public override Stream Inner { get; }

        /**
         * Sync IO: Read
         */
        public override int ReadByte()
        {
            NoteSyncCall();
            return base.ReadByte();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            NoteSyncCall();
            return base.Read(buffer, offset, count);
        }

#if !NETFRAMEWORK
        public override int Read(Span<byte> buffer)
        {
            NoteSyncCall();
            return base.Read(buffer);
        }
#endif

        /**
         * Sync IO: Write
         */
        public override void Write(byte[] buffer, int offset, int count)
        {
            NoteSyncCall();
            base.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            NoteSyncCall();
            base.WriteByte(value);
        }

#if !NETFRAMEWORK
        public override void CopyTo(Stream destination, int bufferSize)
        {
            NoteSyncCall();
            base.CopyTo(destination, bufferSize);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            NoteSyncCall();
            base.Write(buffer);
        }
#endif

        public override void Flush()
        {
            NoteSyncCall();
            base.Flush();
        }

        public override void Close()
        {
            NoteSyncCall();
            base.Close();
        }

#if NETFRAMEWORK
        public ValueTask DisposeAsync()
        {
            return Inner.DisposeAsync();
        }
#endif
    }
}
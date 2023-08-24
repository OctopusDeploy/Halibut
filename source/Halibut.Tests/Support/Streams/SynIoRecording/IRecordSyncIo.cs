using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Halibut.Tests.Support.Streams.SynIoRecording
{
    public interface IRecordSyncIo
    {
        public List<StackTrace> SyncCalls { get; }
    }
}
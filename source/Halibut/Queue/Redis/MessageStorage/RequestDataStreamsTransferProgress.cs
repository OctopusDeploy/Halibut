using System;
using System.Collections.Generic;

namespace Halibut.Queue.Redis.MessageStorage
{
    
    /// <summary>
    /// The transfer progress of DataStreams being sent in a RequestMessage
    /// </summary>
    public class RequestDataStreamsTransferProgress
    {
        public IReadOnlyList<RedisDataStreamTransferProgressRecorder> TransferProgress { get; }

        public RequestDataStreamsTransferProgress(List<RedisDataStreamTransferProgressRecorder> transferProgress)
        {
            this.TransferProgress = transferProgress;
        }
    }
}
using System;
using System.Collections.Generic;
using Halibut.Queue.Redis.MessageStorage;

namespace Halibut.Queue.QueuedDataStreams
{
    public class HeartBeatMessage
    {
        public Dictionary<Guid, long>? DataStreamProgress = new Dictionary<Guid, long>();

        public static HeartBeatMessage Build(
            RequestDataStreamsTransferProgress? transferProgress)
        {

            var dataStreamProgress = new Dictionary<Guid, long>();

            if (transferProgress != null)
            {
                foreach (var dataStreamTransferred in transferProgress.TransferProgress)
                {
                    if(dataStreamTransferred.CopiedSoFar == 0) continue;
                    dataStreamProgress[dataStreamTransferred.DataStramId] = dataStreamTransferred.CopiedSoFar;
                }
            }
            
            return new HeartBeatMessage {DataStreamProgress = dataStreamProgress};
        }
    }
    
}
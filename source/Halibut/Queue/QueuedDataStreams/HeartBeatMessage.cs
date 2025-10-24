using System;
using System.Collections.Generic;
using Halibut.Queue.Redis.MessageStorage;
using Newtonsoft.Json;

namespace Halibut.Queue.QueuedDataStreams
{
    public class HeartBeatMessage
    {
        /// <summary>
        /// Number of bytes of each DataStream has been uploaded to the service.
        /// </summary>
        public IReadOnlyDictionary<Guid, long> DataStreamProgress = new Dictionary<Guid, long>();   

        public static HeartBeatMessage Build(
            RequestDataStreamsTransferProgress? transferProgress)
        {

            var dataStreamProgress = new Dictionary<Guid, long>();

            if (transferProgress != null)
            {
                foreach (var dataStreamTransferred in transferProgress.TransferProgress)
                {
                    if(dataStreamTransferred.CopiedSoFar == 0) continue;
                    dataStreamProgress[dataStreamTransferred.DataStreamId] = dataStreamTransferred.CopiedSoFar;
                }
            }
            
            return new HeartBeatMessage {DataStreamProgress = dataStreamProgress};
        }

        public static string Serialize(HeartBeatMessage heartBeatMessage)
        {
            return JsonConvert.SerializeObject(heartBeatMessage);
        }

        public static HeartBeatMessage Deserialize(string heartBeatMessageJson)
        {
            return JsonConvert.DeserializeObject<HeartBeatMessage>(heartBeatMessageJson) ?? new HeartBeatMessage();
        }
    }
    
}
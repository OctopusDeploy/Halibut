using System;

namespace Halibut.Queue.Redis.MessageStorage
{
    public class RedisStoredMessage
    {
        public RedisStoredMessage(string message, string dataStreamMetadata)
        {
            Message = message;
            DataStreamMetadata = dataStreamMetadata;
        }

        /// <summary>
        /// Either the Request or Response Message
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// Metadata returned by and given to IStoreDataStreamsForDistributedQueues.
        /// This will be stored in Redis alongside the Message. 
        /// </summary>
        public string DataStreamMetadata { get; }
    }
}
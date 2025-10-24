using System;

namespace Halibut.Queue.Redis.MessageStorage
{
    public class RedisStoredMessage
    {
        public RedisStoredMessage(byte[] message, byte[] dataStreamMetadata)
        {
            Message = message;
            DataStreamMetadata = dataStreamMetadata;
        }

        /// <summary>
        /// Either the Request or Response Message
        /// </summary>
        public byte[] Message { get; set; }
        
        /// <summary>
        /// Metadata returned by and given to IStoreDataStreamsForDistributedQueues.
        /// This will be stored in Redis alongside the Message. 
        /// </summary>
        public byte[] DataStreamMetadata { get; }
    }
}
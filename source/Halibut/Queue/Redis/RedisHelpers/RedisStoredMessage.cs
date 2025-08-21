namespace Halibut.Queue.Redis.RedisHelpers
{
    public class RedisStoredMessage
    {
        public RedisStoredMessage(string message, string dataStreamMetadata)
        {
            Message = message;
            DataStreamMetadata = dataStreamMetadata;
        }

        public string Message { get; }
        
        public string DataStreamMetadata { get; }
    }
}
namespace Halibut.Queue.Redis
{
    public class RedisQueueShutdownClientException : HalibutClientException
    {
        public RedisQueueShutdownClientException(string message) : base(message)
        {
        }
    }
}
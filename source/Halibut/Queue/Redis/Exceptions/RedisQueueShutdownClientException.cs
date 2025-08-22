using System;

namespace Halibut.Queue.Redis.Exceptions
{
    public class RedisQueueShutdownClientException : HalibutClientException
    {
        public RedisQueueShutdownClientException(string message) : base(message)
        {
        }
    }
}
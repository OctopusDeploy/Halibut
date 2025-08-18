using System;

namespace Halibut.Queue.Redis
{
    public class RedisDataLoseHalibutClientException : HalibutClientException
    {
        public RedisDataLoseHalibutClientException(string message) : base(message)
        {
        }
    }
}
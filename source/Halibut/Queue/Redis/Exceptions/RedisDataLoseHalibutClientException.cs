using System;

namespace Halibut.Queue.Redis.Exceptions
{
    public class RedisDataLoseHalibutClientException : HalibutClientException
    {
        public RedisDataLoseHalibutClientException(string message) : base(message)
        {
        }
    }
}
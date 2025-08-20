using System;

namespace Halibut.Queue.Redis.Exceptions
{
    public class RedisDataLossHalibutClientException : HalibutClientException
    {
        public RedisDataLossHalibutClientException(string message) : base(message)
        {
        }
    }
}
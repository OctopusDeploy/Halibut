using System;

namespace Halibut.Queue.Redis.Exceptions
{
    public class ErrorOccuredWhenInsertingDataIntoRedisHalibutPendingRequestQueue : HalibutClientException
    {
        public ErrorOccuredWhenInsertingDataIntoRedisHalibutPendingRequestQueue(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
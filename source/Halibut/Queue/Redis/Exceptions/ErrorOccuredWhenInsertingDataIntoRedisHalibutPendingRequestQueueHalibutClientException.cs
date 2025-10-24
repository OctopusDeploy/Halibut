using System;

namespace Halibut.Queue.Redis.Exceptions
{
    public class ErrorOccuredWhenInsertingDataIntoRedisHalibutPendingRequestQueueHalibutClientException : HalibutClientException
    {
        public ErrorOccuredWhenInsertingDataIntoRedisHalibutPendingRequestQueueHalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
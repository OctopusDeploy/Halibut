using System;

namespace Halibut.Queue.Redis.Exceptions
{
    public class ErrorWhilePreparingRequestForQueueHalibutClientException : HalibutClientException
    {
        public ErrorWhilePreparingRequestForQueueHalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
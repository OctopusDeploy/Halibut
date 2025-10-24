using System;

namespace Halibut.Queue.Redis.Exceptions
{
    public class CouldNotGetDataLossTokenInTimeHalibutClientException : HalibutClientException
    {
        public CouldNotGetDataLossTokenInTimeHalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
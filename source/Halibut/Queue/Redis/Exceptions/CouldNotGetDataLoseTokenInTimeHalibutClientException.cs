using System;

namespace Halibut.Queue.Redis.Exceptions
{
    public class CouldNotGetDataLoseTokenInTimeHalibutClientException : HalibutClientException
    {
        public CouldNotGetDataLoseTokenInTimeHalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
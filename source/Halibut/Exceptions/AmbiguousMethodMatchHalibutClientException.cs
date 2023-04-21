using System;

namespace Halibut.Exceptions
{
    public class AmbiguousMethodMatchHalibutClientException : NoMatchingServiceOrMethodHalibutClientException
    {
        public AmbiguousMethodMatchHalibutClientException(string message) : base(message)
        {
        }

        public AmbiguousMethodMatchHalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }

        public AmbiguousMethodMatchHalibutClientException(string message, string serverException) : base(message, serverException)
        {
        }
    }
}
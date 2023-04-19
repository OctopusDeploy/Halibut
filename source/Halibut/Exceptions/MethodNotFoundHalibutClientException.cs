using System;

namespace Halibut.Exceptions
{
    public class MethodNotFoundHalibutClientException : NoMatchingServiceOrMethodHalibutClientException
    {
        public MethodNotFoundHalibutClientException(string message) : base(message)
        {
        }

        public MethodNotFoundHalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }

        public MethodNotFoundHalibutClientException(string message, string serverException) : base(message, serverException)
        {
        }
    }
}
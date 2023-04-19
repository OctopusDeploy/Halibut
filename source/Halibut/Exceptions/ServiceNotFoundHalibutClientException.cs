using System;

namespace Halibut.Exceptions
{
    public class ServiceNotFoundHalibutClientException : NoMatchingServiceOrMethodHalibutClientException
    {
        public ServiceNotFoundHalibutClientException(string message) : base(message)
        {
        }

        public ServiceNotFoundHalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }

        public ServiceNotFoundHalibutClientException(string message, string serverException) : base(message, serverException)
        {
        }
    }
}
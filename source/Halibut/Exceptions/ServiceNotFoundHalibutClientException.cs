using System;

namespace Halibut.Exceptions
{
    public class ServiceNotFoundHalibutClientException : HalibutClientException
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
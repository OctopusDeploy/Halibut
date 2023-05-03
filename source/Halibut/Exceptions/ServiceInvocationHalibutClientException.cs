using System;

namespace Halibut.Exceptions
{
    /// <summary>
    /// Throw when the service itself threw an exception
    /// </summary>
    public class ServiceInvocationHalibutClientException : HalibutClientException
    {
        public ServiceInvocationHalibutClientException(string message) : base(message)
        {
        }

        public ServiceInvocationHalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }

        public ServiceInvocationHalibutClientException(string message, string serverException) : base(message, serverException)
        {
        }
    }
}
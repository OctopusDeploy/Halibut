using System;

namespace Halibut.Exceptions
{
    /// <summary>
    /// Parent exception any time the service (Tentacle) is unable to find the requested
    /// Service or Method. Including when the request from the client is ambiguous.
    /// </summary>
    public class NoMatchingServiceOrMethodHalibutClientException : HalibutClientException
    {
        public NoMatchingServiceOrMethodHalibutClientException(string message) : base(message)
        {
        }

        public NoMatchingServiceOrMethodHalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }

        public NoMatchingServiceOrMethodHalibutClientException(string message, string serverException) : base(message, serverException)
        {
        }
    }
}
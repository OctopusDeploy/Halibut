using System;
using Halibut.Transport;

namespace Halibut
{
    public class HalibutClientException : Exception
    {
        public ConnectionState ConnectionState { get; } = ConnectionState.Unknown;

        public HalibutClientException(string message)
            : base(message)
        {
        }
        
        public HalibutClientException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public HalibutClientException(string message, Exception inner, ConnectionState connectionState)
            : base(message, inner)
        {
            ConnectionState = connectionState;
        }

        public HalibutClientException(string message, string serverException)
            : base(message + Environment.NewLine + Environment.NewLine + "Server exception: " + Environment.NewLine + serverException)
        {
        }
    }
}
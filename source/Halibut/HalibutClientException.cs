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
            : this(message, inner)
        {
            ConnectionState = connectionState;
        }

        public HalibutClientException(string message, string serverException)
            : base(message + Environment.NewLine + Environment.NewLine + "Server exception: " + Environment.NewLine + serverException)
        {
        }

        public HalibutClientException(string message, string serverException, ConnectionState connectionState)
            : this(message, serverException)
        {
            ConnectionState = connectionState;
        }
    }
}
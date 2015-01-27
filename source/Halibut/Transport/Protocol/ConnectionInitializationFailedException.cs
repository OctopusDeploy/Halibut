using System;

namespace Halibut.Transport.Protocol
{
    public class ConnectionInitializationFailedException : Exception
    {
        public ConnectionInitializationFailedException(string message) : base(message)
        {
        }

        public ConnectionInitializationFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public ConnectionInitializationFailedException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }
    }
}
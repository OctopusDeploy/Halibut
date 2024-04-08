using System;

namespace Halibut.Exceptions
{
    public class ActiveTcpConnectionsExceededException : Exception
    {
        public ActiveTcpConnectionsExceededException(string message) : base(message)
        {
        }

        public ActiveTcpConnectionsExceededException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
using System;

namespace Halibut.Transport.Protocol
{
    public class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }
    }
}
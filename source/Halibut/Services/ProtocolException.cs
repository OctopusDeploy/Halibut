using System;

namespace Halibut.Services
{
    public class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }
    }
}
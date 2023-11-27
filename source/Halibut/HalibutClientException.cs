#nullable enable
using System;

namespace Halibut
{
    public class HalibutClientException : Exception
    {
        public Type? InnerExceptionType { get; set; }

        public HalibutClientException(string message)
            : base(message)
        {
        }

        public HalibutClientException(string message, Exception inner)
            : base(message, inner)
        {
            this.InnerExceptionType = inner?.GetType();
        }

        public HalibutClientException(string message, string serverException)
            : base(message + Environment.NewLine + Environment.NewLine + "Server exception: " + Environment.NewLine + serverException)
        {
        }
    }
}
using System;

namespace Halibut
{
    public class HalibutClientException : Exception
    {
        public HalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }

        public HalibutClientException(string message, string serverException)
            : base(message + Environment.NewLine + Environment.NewLine + "Server exception: " + Environment.NewLine + serverException)
        {
        }
    }
}
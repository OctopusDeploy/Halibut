using System;

namespace Halibut.Exceptions
{
    public class ActiveTcpConnectionsExceededException : Exception
    {
        public Uri SubscriptionId { get; }
        
        public ActiveTcpConnectionsExceededException(Uri subscriptionId, string message) : base(message)
        {
            SubscriptionId = subscriptionId;
        }

        public ActiveTcpConnectionsExceededException(Uri subscriptionId, string message, Exception innerException) : base(message, innerException)
        {
            SubscriptionId = subscriptionId;
        }
    }
}
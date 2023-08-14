using System;
using Halibut.Transport;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Support
{
    public class TestConnection : IConnection
    {
        bool hasExpired;

        public int UsageCount { get; private set; }

        public bool Disposed { get; private set; }

        public MessageExchangeProtocol Protocol => null;

        public void Dispose()
        {
            Disposed = true;
        }

        public void NotifyUsed()
        {
            UsageCount++;
        }

        public bool HasExpired()
        {
            return hasExpired;
        }
        
        public void Expire()
        {
            hasExpired = true;
        }
    }
}
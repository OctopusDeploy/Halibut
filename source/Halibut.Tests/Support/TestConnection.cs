using System;
using Halibut.Transport;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Support
{
    public class TestConnection : IConnection
    {
        bool hasExpired;

        public void NotifyUsed()
        {
            UsageCount++;
        }

        public bool HasExpired()
        {
            return hasExpired;
        }

        public int UsageCount { get; private set; }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }

        public MessageExchangeProtocol Protocol => null;

        public void Expire()
        {
            hasExpired = true;
        }
    }
}
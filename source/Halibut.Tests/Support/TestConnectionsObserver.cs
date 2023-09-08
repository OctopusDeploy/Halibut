using System;
using System.Threading;
using Halibut.Transport.Observability;

namespace Halibut.Tests.Support
{
    public class TestConnectionsObserver : IConnectionsObserver
    {
        long connectionAcceptedCount;
        long connectionClosedCount;

        public long ConnectionAcceptedCount => Interlocked.Read(ref connectionAcceptedCount);
        public long ConnectionClosedCount => Interlocked.Read(ref connectionClosedCount);

        public void ConnectionAccepted()
        {
            Interlocked.Increment(ref connectionAcceptedCount);
        }

        public void ConnectionClosed()
        {
            Interlocked.Increment(ref connectionClosedCount);
        }
    }
}
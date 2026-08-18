using System;

namespace Halibut.Transport.Observability
{
    public class NoOpConnectionsObserver : IConnectionsObserver
    {
        static NoOpConnectionsObserver? singleInstance;

        public static NoOpConnectionsObserver Instance => singleInstance ??= new NoOpConnectionsObserver();

        public void ConnectionAccepted(bool authorized)
        {
        }

        public void ConnectionClosed(bool authorized)
        {
        }

        public void ConnectionAcceptedFor(Uri subscriptionId, int currentCount)
        {
        }

        public void ConnectionClosedFor(Uri subscriptionId, int currentCount)
        {
        }
    }
}
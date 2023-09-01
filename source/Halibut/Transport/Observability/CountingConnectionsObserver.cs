using System.Threading;

namespace Halibut.Transport.Observability
{
    public class CountingConnectionsObserver : IConnectionsObserver
    {
        long listenerAcceptedConnectionCount = 0;
        long previouslyAcceptedConnectionHasBeenDisconnectedCount = 0;
        long clientReachedMessageExchangeCount = 0;
        long previouslyAcceptedConnectionFailedToInitializeCount = 0;

        public long ListenerAcceptedConnectionCount => Interlocked.Read(ref listenerAcceptedConnectionCount);
        public long PreviouslyAcceptedConnectionHasBeenDisconnectedCount => Interlocked.Read(ref previouslyAcceptedConnectionHasBeenDisconnectedCount);
        public long ClientReachedMessageExchangeCount => Interlocked.Read(ref clientReachedMessageExchangeCount);
        public long PreviouslyAcceptedConnectionFailedToInitializeCount => Interlocked.Read(ref previouslyAcceptedConnectionFailedToInitializeCount);

        public void ListenerAcceptedConnection()
        {
            Interlocked.Increment(ref listenerAcceptedConnectionCount);
        }

        public void PreviouslyAcceptedConnectionHasBeenDisconnected()
        {
            Interlocked.Increment(ref previouslyAcceptedConnectionHasBeenDisconnectedCount);
        }

        public void ClientReachedMessageExchange()
        {
            Interlocked.Increment(ref clientReachedMessageExchangeCount);
        }

        public void PreviouslyAcceptedConnectionFailedToInitialize()
        {
            Interlocked.Increment(ref previouslyAcceptedConnectionFailedToInitializeCount);
        }
    }
}
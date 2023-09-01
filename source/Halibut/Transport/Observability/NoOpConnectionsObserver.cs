namespace Halibut.Transport.Observability
{
    public class NoOpConnectionsObserver : IConnectionsObserver
    {
        static NoOpConnectionsObserver singleInstance;

        public static NoOpConnectionsObserver Instance()
        {
            if (singleInstance == null)
            {
                singleInstance = new NoOpConnectionsObserver();
            }
            return singleInstance;
        }

        public void ListenerAcceptedConnection()
        {
        }

        public void PreviouslyAcceptedConnectionHasBeenDisconnected()
        {
        }

        public void ClientReachedMessageExchange()
        {
        }

        public void PreviouslyAcceptedConnectionFailedToInitialize()
        {
        }
    }
}
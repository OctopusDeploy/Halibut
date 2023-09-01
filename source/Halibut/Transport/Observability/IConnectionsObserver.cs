namespace Halibut.Transport.Observability
{
    public interface IConnectionsObserver
    {
        /// <summary>
        /// The connection has been accepted and no bytes have been read from the wire.
        ///
        /// In this context server is anything that listens on a port.
        /// 
        /// This is called when any of the following occurs:
        /// - When a "server" accepts a connection from a polling service (either websocket or regular)
        /// - When a "server" accepts a connection from a listening client (so in this case the server is the service)
        /// </summary>
        public void ListenerAcceptedConnection();
        
        /// <summary>
        /// A previously accepted connection has been closed.
        ///
        /// For every call to ListenerAcceptedConnection() their can be at most one call to this method. 
        /// </summary>
        public void PreviouslyAcceptedConnectionHasBeenDisconnected();

        /// <summary>
        /// Called once a polling client is authenticated and we are about to start message exchange
        /// * For polling this means dequeuing messages from the queue
        /// * For listening this means processing the incoming request
        ///
        /// For every call to ListenerAcceptedConnection() their can be at most one call to this method. 
        /// </summary>
        public void ClientReachedMessageExchange();

        /// <summary>
        /// Something went wrong between accepting the connection and reaching the point of message exchange
        ///
        /// For every call to ListenerAcceptedConnection() their can be at most one call to this method. 
        /// </summary>
        void PreviouslyAcceptedConnectionFailedToInitialize();
    }
}
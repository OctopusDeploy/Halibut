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
        public void ConnectionAccepted();

        /// <summary>
        /// A previously accepted connection has been closed.
        ///
        /// For every call to ConnectionClosed() their can be at most one call to this method. 
        /// </summary>
        public void ConnectionClosed();
    }
}
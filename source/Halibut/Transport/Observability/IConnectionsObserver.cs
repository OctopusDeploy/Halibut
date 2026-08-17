using System;

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
        public void ConnectionAccepted(bool authorized);

        /// <summary>
        /// A previously accepted connection has been closed.
        ///
        /// For every call to ConnectionClosed() their can be at most one call to this method.
        /// </summary>
        public void ConnectionClosed(bool authorized);

        /// <summary>
        /// Called once the connection is known to be for a
        /// polling subscriber (i.e. after the subscription id has been read off the wire), and only
        /// for connections that were not rejected for exceeding the active connection limit.
        ///
        /// For every call to this method there will be at most one matching call to ConnectionClosedFor()
        /// with the same subscriptionId.
        /// </summary>
        public void ConnectionAcceptedFor(Uri subscriptionId);

        /// <summary>
        /// A previously accepted polling subscriber connection has been closed.
        /// </summary>
        public void ConnectionClosedFor(Uri subscriptionId);
    }
}
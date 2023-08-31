using System;

namespace Halibut.Diagnostics
{
    public enum EventType
    {
        OpeningNewConnection,
        UsingExistingConnectionFromPool,
        Security,
        MessageExchange,
        Diagnostic,
        
        /// <summary>
        /// Used when the listening server (which may or may not be a service)
        /// is not presented with a certificate from the client OR
        /// the certificate presented by the client is not authorized.
        /// 
        /// This happens before messages are exchanged.
        /// </summary>
        ClientDenied,
        
        Error,
        
        /// <summary>
        /// An error that has occured before message exchange
        /// </summary>
        ErrorInInitialisation,
        
        ListenerStarted,
        ListenerAcceptedClient,
        ListenerStopped,
        SecurityNegotiation,
        FileTransfer,
        
        /// <summary>
        /// Used when an error occurs identifying the remote or identify
        /// This happens before messages are exchanged.
        /// </summary>
        ErrorInIdentify
    }
}
using System;

namespace Halibut.Diagnostics
{
    public enum EventType
    {
        OpeningNewConnection,
        UsingExistingConnectionFromPool,
        SecurityNegotiation,
        Security,
        MessageExchange,
        Diagnostic,
        ClientDenied,
        Error,
        ListenerStarted,
        ListenerAcceptedClient,
        ListenerStopped
    }
}
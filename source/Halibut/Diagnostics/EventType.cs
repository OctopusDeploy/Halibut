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
        ClientDenied,
        Error,
        ListenerStarted,
        ListenerAcceptedClient,
        ListenerStopped,
        SecurityNegotiation
    }
}
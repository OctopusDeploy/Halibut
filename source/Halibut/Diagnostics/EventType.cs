using System;

namespace Halibut.Diagnostics
{
    public enum EventType
    {
        OpeningNewConnection,
        UsingExistingConnectionFromPool,
        Security,
        Diagnostic,
        ClientDenied,
        Error,
        ListenerStarted,
        ListenerAcceptedClient,
        ListenerClosed
    }
}
using System;

namespace Halibut.Tests.Support
{
    public interface IPollingClient
    {
        HalibutRuntime Client { get; }
        int ListeningPort { get; }
        string WebSocketPath { get; }
    }
}
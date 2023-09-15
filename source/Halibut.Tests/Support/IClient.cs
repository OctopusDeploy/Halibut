using System;

namespace Halibut.Tests.Support
{
    public interface IClient
    {
        HalibutRuntime Client { get; }
        int ListeningPort { get; }
        string WebSocketPath { get; }
    }
}
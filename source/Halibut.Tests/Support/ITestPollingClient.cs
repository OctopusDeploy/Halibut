using System;

namespace Halibut.Tests.Support
{
    public interface ITestPollingClient
    {
        int ListeningPort { get; }
        string WebSocketPath { get; }
    }
}
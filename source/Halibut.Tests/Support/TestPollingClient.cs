using System;

namespace Halibut.Tests.Support
{
    public class TestPollingClient : ITestPollingClient
    {
        public int ListeningPort { get; }
        public string WebSocketPath { get; }

        TestPollingClient(int listeningPort, string webSocketPath)
        {
            ListeningPort = listeningPort;
            WebSocketPath = webSocketPath;
        }

        public static TestPollingClient FromPolling(int listeningPort) => new (listeningPort, string.Empty);
        public static TestPollingClient FromPollingOverWebSocket(int listeningPort, string webSocketPath) => new(listeningPort, webSocketPath);
    }
}
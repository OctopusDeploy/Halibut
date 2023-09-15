using System;

namespace Halibut.Tests.Support
{
    public class TestClient : IClient
    {
        public HalibutRuntime Client { get; }
        public int ListeningPort { get; }
        public string WebSocketPath { get; }

        TestClient(HalibutRuntime client, int listeningPort, string webSocketPath)
        {
            Client = client;
            ListeningPort = listeningPort;
            WebSocketPath = webSocketPath;
        }

        public static TestClient FromPolling(HalibutRuntime client, int listeningPort) => new (client, listeningPort, string.Empty);
        public static TestClient FromPollingOverWebSocket(HalibutRuntime client, int listeningPort, string webSocketPath) => new(client, listeningPort, webSocketPath);
    }
}
using System;

namespace Halibut.Tests.Support
{
    public class PollingClient : IPollingClient
    {
        public HalibutRuntime Client { get; }
        public int ListeningPort { get; }
        public string WebSocketPath { get; }

        PollingClient(HalibutRuntime client, int listeningPort, string webSocketPath)
        {
            Client = client;
            ListeningPort = listeningPort;
            WebSocketPath = webSocketPath;
        }

        public static PollingClient FromPolling(HalibutRuntime client, int listeningPort) => new (client, listeningPort, string.Empty);
        public static PollingClient FromPollingOverWebSocket(HalibutRuntime client, int listeningPort, string webSocketPath) => new(client, listeningPort, webSocketPath);
    }
}
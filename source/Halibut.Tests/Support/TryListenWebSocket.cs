using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Halibut.Tests.Support
{
    public class TryListenWebSocket
    {
        public static async Task<ListeningWebSocket> WebSocketListeningPort(ILogger logger, HalibutRuntime client, CancellationToken cancellationToken)
        {
            logger = logger.ForContext<TryListenWebSocket>();
            for (var i = 0; i < 9; i++)
            {
                try
                {
                    return await WebSocketListeningPortAttemptOnce(client, cancellationToken);
                }
                catch (HttpListenerException e)
                {
                    logger.Warning(e, "Failed to listen for websocket, trying again.");
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }
            }

            return await WebSocketListeningPortAttemptOnce(client, cancellationToken);
        }
        static async Task<ListeningWebSocket> WebSocketListeningPortAttemptOnce(HalibutRuntime client, CancellationToken cancellationToken)
        {
            using var tcpPortConflictLock = await TcpPortHelper.WaitForLock(cancellationToken);
            var webSocketListeningPort = TcpPortHelper.FindFreeTcpPort();
            var webSocketPath = Guid.NewGuid().ToString();
            var webSocketListeningUrl = $"https://+:{webSocketListeningPort}/{webSocketPath}";
            var webSocketSslCertificateBindingAddress = $"0.0.0.0:{webSocketListeningPort}";

            client.ListenWebSocket(webSocketListeningUrl);
            return new ListeningWebSocket(webSocketPath, webSocketSslCertificateBindingAddress, webSocketListeningPort);
        }
    }

    public class ListeningWebSocket
    {
        public string WebSocketPath { get; }
        public string WebSocketSslCertificateBindingAddress { get; }
        public int WebSocketListeningPort { get; }

        public ListeningWebSocket(string webSocketPath, string webSocketSslCertificateBindingAddress, int webSocketListeningPort)
        {
            this.WebSocketPath = webSocketPath;
            this.WebSocketSslCertificateBindingAddress = webSocketSslCertificateBindingAddress;
            this.WebSocketListeningPort = webSocketListeningPort;
        }
    }
}
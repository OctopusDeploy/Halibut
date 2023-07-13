using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Halibut.TestProxy
{
    interface IProxyConnection : IDisposable
    {
        Task Connect(TcpClient source, CancellationToken cancellationToken);
    }

    class ProxyConnection : IProxyConnection
    {
        readonly ProxyEndpoint destinationEndpoint;
        readonly ILogger<ProxyConnection> logger;
        List<TcpTunnel>? tunnels = new();
        readonly SemaphoreSlim sync = new(1);

        public ProxyConnection(ProxyEndpoint destinationEndpoint, ILogger<ProxyConnection> logger)
        {
            this.destinationEndpoint = destinationEndpoint;
            this.logger = logger;
        }

        public async Task Connect(TcpClient source, CancellationToken cancellationToken)
        {
            await sync.WaitAsync(cancellationToken);

            try
            {
                if (source.Client.RemoteEndPoint is not IPEndPoint sourceRemoteEndpoint)
                {
                    throw new InvalidOperationException($"{source.Client.RemoteEndPoint} is not an {nameof(IPEndPoint)}");
                }

                var sourceEndpoint = new ProxyEndpoint(sourceRemoteEndpoint.Address.ToString(), sourceRemoteEndpoint.Port);
                var destination = new TcpClient(destinationEndpoint.Hostname, destinationEndpoint.Port);
                var tunnel = new TcpTunnel(source, destination);

                tunnels!.Add(tunnel);

                // We kick the tunneling to a background task so that we can await for it close and cleanup
                _ = Task.Run<Task>(async () =>
                {
                    try
                    {
                        logger.LogInformation("Proxy connection opened - {SourceEndpoint} <-> {DestinationEndpoint}", sourceEndpoint, destinationEndpoint);

                        await tunnel.Tunnel(cancellationToken);

                        logger.LogInformation("Proxy connection closed - {SourceEndpoint} <-> {DestinationEndpoint}", sourceEndpoint, destinationEndpoint);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "An error has occurred in proxy connection - {SourceEndpoint} <-> {DestinationEndpoint}", sourceEndpoint, destinationEndpoint);
                        tunnels.Remove(tunnel);
                        tunnel.Dispose();
                    }
                }, cancellationToken);
            }
            finally
            {
                sync.Release();
            }
        }

        public void Dispose()
        {
            foreach (var tunnel in tunnels!)
            {
                tunnels.Remove(tunnel);
                tunnel.Dispose();
            }

            tunnels = null;

            sync.Dispose();
        }
    }
}
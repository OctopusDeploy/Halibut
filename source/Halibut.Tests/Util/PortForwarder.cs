using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Halibut.Tests.Util
{
    sealed class PortForwarder : IDisposable
    {
        readonly Uri originServer;
        readonly Socket listeningSocket;
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public readonly List<TcpPump> Pumps = new List<TcpPump>();
        readonly ILogger logger = Log.ForContext<PortForwarder>();

        public PortForwarder(Uri originServer)
        {
            this.originServer = originServer;
            var scheme = originServer.Scheme;

            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listeningSocket.Listen(0);
            logger.Information("Listening on {LoadBalancerEndpoint}", listeningSocket.LocalEndPoint?.ToString());

            var port = ((IPEndPoint?)listeningSocket.LocalEndPoint)!.Port;
            PublicEndpoint = new UriBuilder(scheme, "localhost", port).Uri;

            Task.Factory.StartNew(() => WorkerTask(cancellationTokenSource.Token).ConfigureAwait(false), TaskCreationOptions.LongRunning);
        }

        public Uri PublicEndpoint { get; }

        async Task WorkerTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();

                try
                {
                    var clientSocket = await listeningSocket.AcceptAsync();
                    
                    var originEndPoint = new DnsEndPoint(originServer.Host, originServer.Port);
                    var originSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

                    var pump = new TcpPump(clientSocket, originSocket, originEndPoint);
                    pump.Stopped += OnPortForwarderStopped;
                    lock (Pumps)
                    {
                        Pumps.Add(pump);
                    }

                    pump.Start();
                }
                catch (SocketException ex)
                {
                    // This will occur normally on teardown.
                    logger.Verbose(ex, "Socket Error accepting new connection {Message}", ex.Message);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error accepting new connection {Message}", ex.Message);
                }
            }
        }

        void OnPortForwarderStopped(object? sender, EventArgs e)
        {
            if (sender is TcpPump portForwarder)
            {
                portForwarder.Stopped -= OnPortForwarderStopped;
                lock (Pumps)
                {
                    Pumps.Remove(portForwarder);
                }

                portForwarder.Dispose();
            }
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            listeningSocket.Close();

            lock (Pumps)
            {
                var clone = Pumps.ToArray();
                Pumps.Clear();
                foreach (var portForwarder in clone)
                {
                    portForwarder.Dispose();
                }
            }

            listeningSocket.Dispose();
            cancellationTokenSource.Dispose();
        }
    }
}
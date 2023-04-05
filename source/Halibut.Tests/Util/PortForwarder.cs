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
        readonly TimeSpan sendDelay;

        public PortForwarder(Uri originServer, TimeSpan sendDelay, int? listeningPort = null)
        {
            this.originServer = originServer;
            this.sendDelay = sendDelay;
            var scheme = originServer.Scheme;

            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.Bind(new IPEndPoint(IPAddress.Loopback, listeningPort ?? 0));
            listeningSocket.Listen(0);
            logger.Information("Listening on {LoadBalancerEndpoint}", listeningSocket.LocalEndPoint?.ToString());

            var port = ((IPEndPoint)listeningSocket.LocalEndPoint).Port;
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

                    var pump = new TcpPump(clientSocket, originSocket, originEndPoint, sendDelay);
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

        void OnPortForwarderStopped(object sender, EventArgs e)
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
            if(!cancellationTokenSource.IsCancellationRequested) cancellationTokenSource.Cancel();
            listeningSocket.Close();

            var exceptions = new List<Exception>();
            lock (Pumps)
            {
                var clone = Pumps.ToArray();
                Pumps.Clear();
                foreach (var portForwarder in clone)
                {
                    try
                    {
                        portForwarder.Dispose();
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }
            }

            try
            {
                listeningSocket.Dispose();
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }

            try
            {
                cancellationTokenSource.Dispose();
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }
    }
}
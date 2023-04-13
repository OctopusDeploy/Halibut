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
        readonly List<TcpPump> pumps = new List<TcpPump>();
        readonly ILogger logger = Log.ForContext<PortForwarder>();
        readonly TimeSpan sendDelay;

        public PortForwarder(Uri originServer, TimeSpan sendDelay)
        {
            this.originServer = originServer;
            this.sendDelay = sendDelay;
            var scheme = originServer.Scheme;

            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
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
                    lock (pumps)
                    {
                        pumps.Add(pump);
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
                lock (pumps)
                {
                    pumps.Remove(portForwarder);
                }

                portForwarder.Dispose();
            }
        }

        public void PauseExistingConnections()
        {
            lock (pumps)
            {
                foreach (var pump in pumps)
                {
                    pump.Pause();
                }
            }
        }

        public void CloseExistingConnections()
        {
            DisposePumps();
        }

        List<Exception> DisposePumps()
        {
            var exceptions = new List<Exception>();

            lock (pumps)
            {
                var clone = pumps.ToArray();
                pumps.Clear();
                foreach (var pump in clone)
                {
                    try
                    {
                        pump.Dispose();
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }
            }

            return exceptions;
        }

        public void Dispose()
        {
            if(!cancellationTokenSource.IsCancellationRequested) cancellationTokenSource.Cancel();
            listeningSocket.Close();

            var exceptions = DisposePumps();

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
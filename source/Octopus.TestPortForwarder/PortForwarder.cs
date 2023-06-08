using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Octopus.TestPortForwarder
{
    public class PortForwarder : IDisposable
    {
        readonly Uri originServer;
        Socket? listeningSocket;
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly List<TcpPump> pumps = new();
        readonly ILogger logger;
        readonly TimeSpan sendDelay;
        Func<BiDirectionalDataTransferObserver> factory;
        bool active = false;

        public int ListeningPort { get; }

        public PortForwarder(Uri originServer,
            TimeSpan sendDelay,
            Func<BiDirectionalDataTransferObserver> factory,
            ILogger logger,
            int? listeningPort = null)
        {
            logger = logger.ForContext<PortForwarder>();
            this.originServer = originServer;
            this.sendDelay = sendDelay;
            this.factory = factory;
            this.logger = logger;
            var scheme = originServer.Scheme;

            Start();

            ListeningPort = ((IPEndPoint)listeningSocket.LocalEndPoint).Port;
            PublicEndpoint = new UriBuilder(scheme, "localhost", ListeningPort).Uri;

            Task.Factory.StartNew(() => WorkerTask(cancellationTokenSource.Token).ConfigureAwait(false), TaskCreationOptions.LongRunning);
        }

        private void Start()
        {
            if (active)
            {
                throw new InvalidOperationException("PortForwarder is already started");
            }

            listeningSocket ??= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listeningSocket!.Bind(new IPEndPoint(IPAddress.Loopback, ListeningPort));

            try
            {
                listeningSocket.Listen(int.MaxValue);
            }
            catch (SocketException)
            {
                Stop();
                throw;
            }
            logger.Information("Listening on {LoadBalancerEndpoint}", listeningSocket.LocalEndPoint?.ToString());
            active = true;
        }

        private void Stop()
        {
            active = false;
            listeningSocket?.Dispose();
            listeningSocket = null;
            logger.Information("Stopped listening");
            CloseExistingConnections();
        }

        public Uri PublicEndpoint { get; set; }

        public bool KillNewConnectionsImmediatlyMode { get; set; }

        public void EnterKillNewAndExistingConnectionsMode()
        {
            KillNewConnectionsImmediatlyMode = true;
            this.CloseExistingConnections();
        }
        
        public void ReturnToNormalMode()
        {
            KillNewConnectionsImmediatlyMode = false;
        }

        async Task WorkerTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
                if (active)
                {
                    try
                    {
                        var clientSocket = await listeningSocket?.AcceptAsync();

                        if (!active || KillNewConnectionsImmediatlyMode || cancellationToken.IsCancellationRequested)
                        {

                            CloseSocketIgnoringErrors(clientSocket);

                            if(!active) throw new OperationCanceledException("Port forwarder is not active");
                            continue;
                        }

                        var originEndPoint = new DnsEndPoint(originServer.Host, originServer.Port);
                        var originSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

                        var pump = new TcpPump(clientSocket, originSocket, originEndPoint, sendDelay, factory, logger);
                        AddNewPump(pump, cancellationToken);
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
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }
            }
        }

        private static void CloseSocketIgnoringErrors(Socket clientSocket)
        {
            DoIgnoringException(() => clientSocket.Shutdown(SocketShutdown.Both));
            DoIgnoringException(() => clientSocket.Close(0));
            DoIgnoringException(() => clientSocket.Dispose());
        }

        private void AddNewPump(TcpPump pump, CancellationToken cancellationToken)
        {
            
            lock (pumps)
            {
                if (cancellationToken.IsCancellationRequested || !active || KillNewConnectionsImmediatlyMode)
                {
                    try
                    {
                        pump.Dispose();
                    }
                    catch (Exception)
                    {
                        
                    }
                }
                else
                {
                    pump.Stopped += OnPortForwarderStopped;
                    pumps.Add(pump);
                }
            }

            pump.Start();
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

        public void UnPauseExistingConnections()
        {
            lock (pumps)
            {
                foreach (var pump in pumps)
                {
                    pump.UnPause();
                }
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
            logger.Information("Closing existing connections");
            DisposePumps();
        }

        List<Exception> DisposePumps()
        {
            logger.Information("Start Dispose Pumps");
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

            logger.Information("Fisinshed Dispose Pumps");

            return exceptions;
        }

        public void Dispose()
        {
            if(!cancellationTokenSource.IsCancellationRequested) cancellationTokenSource.Cancel();

            var exceptions = DisposePumps();

            try
            {
                listeningSocket?.Shutdown(SocketShutdown.Both);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }

            try
            {
                listeningSocket?.Close(0);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }

            try
            {
                listeningSocket?.Dispose();
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

            if (exceptions.Count(x => x is not ObjectDisposedException &&
                    !(x is SocketException && x.Message.Contains("A request to send or receive data was disallowed because the socket is not connected"))) > 0)
            {
                logger.Warning(new AggregateException(exceptions), "Exceptions where thrown when Disposing of the PortForwarder");
            }
        }

        private static void DoIgnoringException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
            }
        }
    }
}
